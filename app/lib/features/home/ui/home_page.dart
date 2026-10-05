import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/config/app_config.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/router/app_router.dart';
import '../../campaigns/ui/campaigns_page.dart';
import '../data/server_info_repository.dart';

enum _HomeAction { library, adminUsers, server, logout }

/// Landing page after login: the list of campaigns, with the user menu in the
/// app bar and the server status as a footer.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  void _onAction(BuildContext context, WidgetRef ref, _HomeAction action) {
    switch (action) {
      case _HomeAction.library:
        context.push(AppRoutes.library);
      case _HomeAction.adminUsers:
        context.push(AppRoutes.adminUsers);
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
              onSelected: (action) => _onAction(context, ref, action),
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
      body: const Column(
        children: [
          Expanded(child: CampaignsPage()),
          _ServerStatus(),
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
