import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/config/app_config.dart';
import '../../../core/router/app_router.dart';
import '../data/server_info_repository.dart';

/// Landing page after login: shows the user, their role and the server status.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthSignedIn ? auth.user : null;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text(AppConfig.appName)),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.casino_outlined, size: 72),
              const SizedBox(height: 16),
              if (user != null) ...[
                Text(
                  'Hola, ${user.displayName}',
                  key: const Key('home-greeting'),
                  style: theme.textTheme.headlineSmall,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 4),
                Text(
                  user.role.label,
                  key: const Key('home-role'),
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 24),
                if (user.isAdmin) ...[
                  FilledButton.tonalIcon(
                    key: const Key('home-admin-users'),
                    onPressed: () => context.push(AppRoutes.adminUsers),
                    icon: const Icon(Icons.group_outlined),
                    label: const Text('Usuarios'),
                  ),
                  const SizedBox(height: 12),
                ],
                OutlinedButton.icon(
                  key: const Key('home-logout'),
                  onPressed: () => ref.read(authControllerProvider.notifier).logout(),
                  icon: const Icon(Icons.logout),
                  label: const Text('Cerrar sesión'),
                ),
                const SizedBox(height: 32),
              ],
              const _ServerStatus(),
            ],
          ),
        ),
      ),
    );
  }
}

/// Connection indicator backed by `/api/v1/app/info`.
class _ServerStatus extends ConsumerWidget {
  const _ServerStatus();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverInfo = ref.watch(serverInfoProvider);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          'Servidor: ${AppConfig.apiBaseUrl}',
          style: Theme.of(context).textTheme.bodySmall,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        serverInfo.when(
          loading: () => const CircularProgressIndicator(),
          data: (info) => Text(
            'Conectado a ${info.name} v${info.version}',
            key: const Key('server-status'),
            textAlign: TextAlign.center,
          ),
          error: (error, _) => Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                'No se pudo conectar con el servidor.',
                key: const Key('server-status'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: () => ref.invalidate(serverInfoProvider),
                icon: const Icon(Icons.refresh),
                label: const Text('Reintentar'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
