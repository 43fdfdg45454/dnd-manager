import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/auth/user_dto.dart';
import '../../../core/cache/cache_maintenance.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/update/update_ui.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/server_info_repository.dart';
import 'edit_name_dialog.dart';

/// "Perfil" tab: the account (name, emails), the app (appearance, updates,
/// cache, server), the administration and the attributions, as a list. It
/// replaces the user menu of the old home page.
class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

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

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthSignedIn ? auth.user : null;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Perfil')),
      body: user == null
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              key: const Key('profile-list'),
              padding: const EdgeInsets.only(bottom: 24),
              children: [
                ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.person_outline)),
                  title: Text(user.displayName, key: const Key('home-greeting')),
                  subtitle: Text(
                    '${user.email} · ${user.role.label}',
                    key: const Key('home-role'),
                  ),
                ),
                _Header('Cuenta', theme),
                ListTile(
                  key: const Key('home-edit-name'),
                  leading: const Icon(Icons.edit_outlined),
                  title: const Text('Editar nombre'),
                  onTap: () => _editName(context, ref, user.displayName),
                ),
                SwitchListTile(
                  key: const Key('home-notifications'),
                  secondary: const Icon(Icons.mail_outline),
                  title: const Text('Recibir correos'),
                  subtitle: const Text('Avisos de sesiones de tus campañas'),
                  value: user.notificationsEnabled,
                  onChanged: (value) => _toggleNotifications(context, ref, value),
                ),
                _Header('Aplicación', theme),
                ListTile(
                  key: const Key('home-appearance'),
                  leading: const Icon(Icons.palette_outlined),
                  title: const Text('Personalización'),
                  subtitle: const Text('Paleta, modo oscuro y fuentes'),
                  onTap: () => context.push(AppRoutes.appearance),
                ),
                ListTile(
                  key: const Key('home-check-updates'),
                  leading: const Icon(Icons.system_update_alt_outlined),
                  title: const Text('Buscar actualizaciones'),
                  onTap: () => checkForUpdatesFromMenu(context, ref),
                ),
                ListTile(
                  key: const Key('home-clear-cache'),
                  leading: const Icon(Icons.cleaning_services_outlined),
                  title: const Text('Vaciar caché'),
                  onTap: () => _clearCache(context, ref),
                ),
                ListTile(
                  key: const Key('home-server'),
                  leading: const Icon(Icons.dns_outlined),
                  title: const Text('Servidor'),
                  subtitle: Text(ref.watch(serverConfigProvider).baseUrl),
                  onTap: () => context.push(AppRoutes.server),
                ),
                const _ServerStatusTile(),
                if (user.isAdmin) ...[
                  _Header('Administración', theme),
                  ListTile(
                    key: const Key('home-admin-users'),
                    leading: const Icon(Icons.manage_accounts_outlined),
                    title: const Text('Usuarios'),
                    onTap: () => context.push(AppRoutes.adminUsers),
                  ),
                  ListTile(
                    key: const Key('home-admin-content'),
                    leading: const Icon(Icons.inventory_2_outlined),
                    title: const Text('Contenido'),
                    subtitle: const Text('Paquetes de contenido y versiones de la app'),
                    onTap: () => context.push(AppRoutes.adminContent),
                  ),
                ],
                _Header('Acerca de', theme),
                ListTile(
                  key: const Key('home-attribution'),
                  leading: const Icon(Icons.info_outline),
                  title: const Text('Atribuciones'),
                  onTap: () => context.push(AppRoutes.attributions),
                ),
                const Divider(),
                ListTile(
                  key: const Key('home-logout'),
                  leading: Icon(Icons.logout, color: theme.colorScheme.error),
                  title: Text('Cerrar sesión', style: TextStyle(color: theme.colorScheme.error)),
                  onTap: () => ref.read(authControllerProvider.notifier).logout(),
                ),
              ],
            ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header(this.text, this.theme);

  final String text;
  final ThemeData theme;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
    child: Text(
      text,
      style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.primary),
    ),
  );
}

/// Compact connection indicator backed by `/api/v1/app/info`.
class _ServerStatusTile extends ConsumerWidget {
  const _ServerStatusTile();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverInfo = ref.watch(serverInfoProvider);
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(72, 0, 16, 8),
      child: serverInfo.when(
        loading: () => const SizedBox(
          width: 16,
          height: 16,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
        data: (info) => Text(
          'Conectado a ${info.name} v${info.version}',
          key: const Key('server-status'),
          style: theme.textTheme.bodySmall,
        ),
        error: (error, _) => Row(
          children: [
            Flexible(
              child: Text(
                'No se pudo conectar con el servidor.',
                key: const Key('server-status'),
                style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
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
    );
  }
}

/// The signed-in user, for pages that only need to know who they are.
UserDto? currentUser(WidgetRef ref) {
  final auth = ref.watch(authControllerProvider);
  return auth is AuthSignedIn ? auth.user : null;
}
