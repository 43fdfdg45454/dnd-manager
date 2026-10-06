import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/config/app_config.dart';
import '../../../core/auth/user_dto.dart';
import '../../../core/cache/cache_maintenance.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../../core/update/update_ui.dart';
import '../../campaigns/data/campaigns_repository.dart';
import '../../campaigns/ui/campaigns_page.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../../sessions/data/sessions_repository.dart';
import '../../sessions/ui/next_session_card.dart';
import 'edit_name_dialog.dart';
import '../data/server_info_repository.dart';

enum _HomeAction {
  editName,
  notifications,
  library,
  adminUsers,
  adminContent,
  clearCache,
  checkUpdates,
  appearance,
  attributions,
  server,
  logout,
}

/// Landing page after login: the list of campaigns, with the user menu in the
/// app bar and the server status as a footer.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  Future<void> _editName(BuildContext context, WidgetRef ref, String current) async {
    final name = await showDialog<String>(
      context: context,
      builder: (_) => EditNameDialog(initialName: current),
    );
    if (name == null || name == current || !context.mounted) return;
    await runAction(
      context,
      () => ref.read(authControllerProvider.notifier).updateProfile(displayName: name),
      success: 'Nombre actualizado.',
      errors: const {400: 'El nombre no es válido.'},
      describe: describeApiError,
    );
  }

  Future<void> _toggleNotifications(BuildContext context, WidgetRef ref, bool enabled) => runAction(
    context,
    () => ref.read(authControllerProvider.notifier).updateProfile(notificationsEnabled: enabled),
    success: enabled ? 'Recibirás correos de la campaña.' : 'No recibirás correos de la campaña.',
    describe: describeApiError,
  );

  /// Empties the offline data after confirming; the screens reload it.
  Future<void> _clearCache(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Vaciar caché',
      message:
          'Se borrarán los datos guardados para usar la app sin conexión y las imágenes '
          'descargadas. Los PDF descargados de la biblioteca se conservan.',
      confirmLabel: 'Vaciar',
    );
    if (!confirmed || !context.mounted) return;
    await ref.read(cacheMaintenanceProvider).clearAll();
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Caché vaciada.')));
  }

  void _onAction(BuildContext context, WidgetRef ref, _HomeAction action, UserDto user) {
    switch (action) {
      case _HomeAction.editName:
        _editName(context, ref, user.displayName);
      case _HomeAction.notifications:
        _toggleNotifications(context, ref, !user.notificationsEnabled);
      case _HomeAction.library:
        context.push(AppRoutes.library);
      case _HomeAction.adminUsers:
        context.push(AppRoutes.adminUsers);
      case _HomeAction.adminContent:
        context.push(AppRoutes.adminContent);
      case _HomeAction.clearCache:
        _clearCache(context, ref);
      case _HomeAction.checkUpdates:
        checkForUpdatesFromMenu(context, ref);
      case _HomeAction.appearance:
        context.push(AppRoutes.appearance);
      case _HomeAction.attributions:
        context.push(AppRoutes.attributions);
      case _HomeAction.server:
        context.push(AppRoutes.server);
      case _HomeAction.logout:
        ref.read(authControllerProvider.notifier).logout();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthSignedIn ? auth.user : null;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text(AppConfig.appName),
        actions: [
          IconButton(
            key: const Key('home-compendium'),
            tooltip: 'Compendio',
            icon: const Icon(Icons.menu_book_outlined),
            onPressed: () => context.push(AppRoutes.compendium),
          ),
          if (user != null)
            PopupMenuButton<_HomeAction>(
              key: const Key('home-user-menu'),
              tooltip: 'Menú de usuario',
              onSelected: (action) => _onAction(context, ref, action, user),
              itemBuilder: (_) => [
                PopupMenuItem(
                  enabled: false,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Hola, ${user.displayName}',
                        key: const Key('home-greeting'),
                        style: theme.textTheme.titleSmall,
                      ),
                      Text(
                        user.role.label,
                        key: const Key('home-role'),
                        style: theme.textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
                const PopupMenuDivider(),
                const PopupMenuItem(
                  key: Key('home-edit-name'),
                  value: _HomeAction.editName,
                  child: Text('Editar nombre'),
                ),
                PopupMenuItem(
                  key: const Key('home-notifications'),
                  value: _HomeAction.notifications,
                  child: Row(
                    children: [
                      const Expanded(child: Text('Recibir correos')),
                      IgnorePointer(
                        child: Switch(
                          key: const Key('home-notifications-switch'),
                          value: user.notificationsEnabled,
                          onChanged: (_) {},
                        ),
                      ),
                    ],
                  ),
                ),
                const PopupMenuDivider(),
                const PopupMenuItem(
                  key: Key('home-library'),
                  value: _HomeAction.library,
                  child: Text('Biblioteca'),
                ),
                if (user.isAdmin)
                  const PopupMenuItem(
                    key: Key('home-admin-users'),
                    value: _HomeAction.adminUsers,
                    child: Text('Usuarios'),
                  ),
                if (user.isAdmin)
                  const PopupMenuItem(
                    key: Key('home-admin-content'),
                    value: _HomeAction.adminContent,
                    child: Text('Contenido'),
                  ),
                const PopupMenuItem(
                  key: Key('home-clear-cache'),
                  value: _HomeAction.clearCache,
                  child: Text('Vaciar caché'),
                ),
                const PopupMenuItem(
                  key: Key('home-check-updates'),
                  value: _HomeAction.checkUpdates,
                  child: Text('Buscar actualizaciones'),
                ),
                const PopupMenuItem(
                  key: Key('home-appearance'),
                  value: _HomeAction.appearance,
                  child: Text('Personalización'),
                ),
                const PopupMenuItem(
                  key: Key('home-attribution'),
                  value: _HomeAction.attributions,
                  child: Text('Atribuciones'),
                ),
                const PopupMenuItem(
                  key: Key('home-server'),
                  value: _HomeAction.server,
                  child: Text('Servidor'),
                ),
                const PopupMenuItem(
                  key: Key('home-logout'),
                  value: _HomeAction.logout,
                  child: Text('Cerrar sesión'),
                ),
              ],
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.account_circle_outlined),
                    const SizedBox(width: 6),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 140),
                      child: Text(user.displayName, overflow: TextOverflow.ellipsis),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
      body: Column(
        children: [
          OfflineBanner(
            scopes: [
              staleExact(CampaignsRepository.listPath),
              staleTree(SessionsRepository.mySessionsPath),
            ],
          ),
          const NextSessionCard(),
          const Expanded(child: CampaignsPage()),
          const _ServerStatus(),
        ],
      ),
    );
  }
}

/// Compact connection indicator backed by `/api/v1/app/info`.
class _ServerStatus extends ConsumerWidget {
  const _ServerStatus();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverInfo = ref.watch(serverInfoProvider);
    final theme = Theme.of(context);
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Servidor: ${ref.watch(serverConfigProvider).baseUrl}',
              style: theme.textTheme.bodySmall,
              textAlign: TextAlign.center,
            ),
            serverInfo.when(
              loading: () => const Padding(
                padding: EdgeInsets.only(top: 4),
                child: SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
              data: (info) => Text(
                'Conectado a ${info.name} v${info.version}',
                key: const Key('server-status'),
                style: theme.textTheme.bodySmall,
                textAlign: TextAlign.center,
              ),
              error: (error, _) => Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Flexible(
                    child: Text(
                      'No se pudo conectar con el servidor.',
                      key: const Key('server-status'),
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  TextButton.icon(
                    onPressed: () => ref.invalidate(serverInfoProvider),
                    icon: const Icon(Icons.refresh, size: 18),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
