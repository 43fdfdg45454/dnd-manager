import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theme/app_icon.dart';
import '../theme/icons.dart';
import '../theme/tokens.dart';
import 'realtime_hub.dart';
import 'realtime_provider.dart';

/// Realtime connection status of a campaign in its app bar
/// (`Key('realtime-status')`): golden sparkles when connected, grey while
/// connecting or reconnecting, a crossed-out cloud without connection.
class RealtimeStatusIcon extends ConsumerWidget {
  const RealtimeStatusIcon({super.key, required this.campaignId});

  final String campaignId;

  static String tooltipFor(RealtimeStatus status) => switch (status) {
    RealtimeStatus.connected => 'Tiempo real: conectado',
    RealtimeStatus.connecting => 'Tiempo real: conectando…',
    RealtimeStatus.reconnecting => 'Tiempo real: reconectando…',
    RealtimeStatus.disconnected => 'Tiempo real: sin conexión con el servidor',
    RealtimeStatus.offline => 'Tiempo real: sin red',
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = ref.watch(campaignRealtimeProvider(campaignId));
    final tokens = context.tokens;
    final label = tooltipFor(status);
    final icon = switch (status) {
      RealtimeStatus.connected => AppIcon(
        AppIcons.sparkles,
        key: const Key('realtime-connected'),
        color: tokens.gold,
        size: 22,
        semanticLabel: label,
      ),
      RealtimeStatus.connecting || RealtimeStatus.reconnecting => AppIcon(
        AppIcons.sparkles,
        key: const Key('realtime-reconnecting'),
        color: tokens.inkMuted.withValues(alpha: 0.6),
        size: 22,
        semanticLabel: label,
      ),
      RealtimeStatus.disconnected || RealtimeStatus.offline => Icon(
        Icons.cloud_off,
        key: const Key('realtime-offline'),
        color: tokens.inkMuted,
        size: 22,
        semanticLabel: label,
      ),
    };
    return Tooltip(
      key: const Key('realtime-status'),
      message: label,
      child: Padding(padding: const EdgeInsets.symmetric(horizontal: 8), child: icon),
    );
  }
}
