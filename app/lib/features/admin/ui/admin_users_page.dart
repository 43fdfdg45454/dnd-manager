import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/auth/user_dto.dart';
import '../../../core/network/api_error.dart';
import '../data/admin_users_controller.dart';
import 'create_user_dialog.dart';

enum _UserAction { resendSetup, toggleActive, changeRole }

class AdminUsersPage extends ConsumerStatefulWidget {
  const AdminUsersPage({super.key});

  @override
  ConsumerState<AdminUsersPage> createState() => _AdminUsersPageState();
}

class _AdminUsersPageState extends ConsumerState<AdminUsersPage> {
  final _searchController = TextEditingController();
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  AdminUsersController get _controller => ref.read(adminUsersControllerProvider.notifier);

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () => _controller.search(value));
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  /// Runs [action], showing [success] or a Spanish error message in a SnackBar.
  Future<void> _run(
    Future<void> Function() action, {
    String? success,
    Map<int, String> errors = const {},
  }) async {
    try {
      await action();
      if (mounted && success != null) _showMessage(success);
    } catch (error) {
      if (mounted) _showMessage(describeApiError(error, byStatus: errors));
    }
  }

  Future<void> _createUser() async {
    final data = await showDialog<NewUserData>(
      context: context,
      builder: (_) => const CreateUserDialog(),
    );
    if (data == null) return;
    await _run(
      () => _controller.create(email: data.email, displayName: data.displayName, role: data.role),
      success: 'Usuario creado. Se ha enviado el correo de alta.',
      errors: const {
        409: 'Ya existe un usuario con ese correo.',
        400: 'Datos no válidos. Revisa el correo y el nombre.',
      },
    );
  }

  Future<void> _onAction(_UserAction action, UserDto user) async {
    switch (action) {
      case _UserAction.resendSetup:
        await _run(
          () => _controller.resendSetupEmail(user.id),
          success: 'Correo de alta reenviado a ${user.email}.',
        );
      case _UserAction.toggleActive:
        await _run(
          () => _controller.updateUser(user.id, isActive: !user.isActive),
          success: user.isActive ? 'Usuario desactivado.' : 'Usuario activado.',
          errors: const {400: 'No puedes desactivarte a ti mismo.'},
        );
      case _UserAction.changeRole:
        final role = await showDialog<UserRole>(
          context: context,
          builder: (_) => SimpleDialog(
            title: Text('Rol de ${user.displayName}'),
            children: [
              for (final r in UserRole.values)
                SimpleDialogOption(
                  onPressed: () => Navigator.of(context).pop(r),
                  child: Row(
                    children: [
                      Icon(r == user.role ? Icons.radio_button_checked : Icons.radio_button_off),
                      const SizedBox(width: 12),
                      Text(r.label),
                    ],
                  ),
                ),
            ],
          ),
        );
        if (role == null || role == user.role) return;
        await _run(
          () => _controller.updateUser(user.id, role: role),
          success: 'Rol actualizado.',
          errors: const {400: 'No puedes quitarte a ti mismo el rol de administrador.'},
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final users = ref.watch(adminUsersControllerProvider);
    final auth = ref.watch(authControllerProvider);
    final currentUserId = auth is AuthSignedIn ? auth.user.id : null;

    return Scaffold(
      appBar: AppBar(title: const Text('Usuarios')),
      floatingActionButton: FloatingActionButton.extended(
        key: const Key('admin-new-user'),
        onPressed: _createUser,
        icon: const Icon(Icons.person_add_alt_1),
        label: const Text('Nuevo usuario'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: TextField(
              key: const Key('admin-search'),
              controller: _searchController,
              onChanged: _onSearchChanged,
              textInputAction: TextInputAction.search,
              decoration: const InputDecoration(
                hintText: 'Buscar por nombre o correo',
                prefixIcon: Icon(Icons.search),
                border: OutlineInputBorder(),
              ),
            ),
          ),
          Expanded(
            child: users.when(
              skipLoadingOnReload: true,
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(describeApiError(error), textAlign: TextAlign.center),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: () => ref.invalidate(adminUsersControllerProvider),
                      icon: const Icon(Icons.refresh),
                      label: const Text('Reintentar'),
                    ),
                  ],
                ),
              ),
              data: (page) {
                if (page.items.isEmpty) {
                  return const Center(child: Text('No se encontraron usuarios.'));
                }
                return ListView(
                  padding: const EdgeInsets.only(bottom: 88),
                  children: [
                    for (final user in page.items)
                      _UserTile(
                        user: user,
                        isSelf: user.id == currentUserId,
                        onAction: (action) => _onAction(action, user),
                      ),
                    if (page.hasMore)
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: OutlinedButton(
                          onPressed: () => _run(_controller.loadMore),
                          child: const Text('Cargar más'),
                        ),
                      ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _UserTile extends StatelessWidget {
  const _UserTile({required this.user, required this.isSelf, required this.onAction});

  final UserDto user;
  final bool isSelf;
  final ValueChanged<_UserAction> onAction;

  @override
  Widget build(BuildContext context) {
    final details = [
      user.role.label,
      if (!user.isActive) 'Inactivo',
      if (!user.hasPassword) 'Pendiente de alta',
    ].join(' · ');

    return ListTile(
      key: Key('user-${user.id}'),
      leading: CircleAvatar(
        child: Text(user.displayName.isEmpty ? '?' : user.displayName[0].toUpperCase()),
      ),
      title: Text(
        user.displayName,
        style: user.isActive ? null : const TextStyle(decoration: TextDecoration.lineThrough),
      ),
      subtitle: Text('${user.email}\n$details'),
      isThreeLine: true,
      trailing: PopupMenuButton<_UserAction>(
        key: Key('user-menu-${user.id}'),
        onSelected: onAction,
        itemBuilder: (_) => [
          const PopupMenuItem(
            value: _UserAction.resendSetup,
            child: Text('Reenviar correo de alta'),
          ),
          // The API rejects an admin demoting or deactivating themselves.
          PopupMenuItem(
            value: _UserAction.toggleActive,
            enabled: !isSelf,
            child: Text(user.isActive ? 'Desactivar' : 'Activar'),
          ),
          PopupMenuItem(
            value: _UserAction.changeRole,
            enabled: !isSelf,
            child: const Text('Cambiar rol'),
          ),
        ],
      ),
    );
  }
}
