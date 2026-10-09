import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_repository.dart';
import '../../campaigns/ui/campaigns_page.dart';
import '../../sessions/data/sessions_repository.dart';
import '../../sessions/ui/next_session_card.dart';
import '../data/server_info_repository.dart';

/// "Campañas" tab: the list of campaigns and the invitations, the next session
/// and the server status as a footer. The account lives in the "Perfil" tab.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      key: const Key('home-page'),
      appBar: AppBar(title: const Text(AppConfig.appName)),
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
