import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../motion/pulse.dart';
import '../theme/app_icon.dart';
import '../theme/icons.dart';
import '../theme/tokens.dart';
import 'realtime_hub.dart';
import 'realtime_provider.dart';

/// Realtime connection status of a campaign in its app bar
/// (`Key('realtime-status')`): a golden rune seal that beats ([PulseSeal],
/// "En vivo") when connected and the same seal, still and grey, otherwise (the
/// banner under the app bar explains why).
class RealtimeStatusIcon extends ConsumerWidget {
  const RealtimeStatusIcon({super.key, required this.campaignId});

  final String campaignId;

  static String tooltipFor(RealtimeStatus status) => switch (status) {
    RealtimeStatus.connected => 'En vivo',
    RealtimeStatus.connecting => 'Tiempo real: conectando…',
    RealtimeStatus.reconnecting => 'Tiempo real: reconectando…',
    RealtimeStatus.disconnected => 'Tiempo real: sin conexión con el servidor',
    RealtimeStatus.offline => 'Tiempo real: sin red',
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = ref.watch(campaignRealtimeProvider(campaignId).select((s) => s.status));
    final tokens = context.tokens;
    final label = tooltipFor(status);
    final connected = status == RealtimeStatus.connected;
    final key = switch (status) {
      RealtimeStatus.connected => const Key('realtime-connected'),
      RealtimeStatus.connecting ||
      RealtimeStatus.reconnecting => const Key('realtime-reconnecting'),
      RealtimeStatus.disconnected || RealtimeStatus.offline => const Key('realtime-offline'),
    };
    return Tooltip(
      key: const Key('realtime-status'),
      message: label,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8),
        child: connected
            ? PulseSeal(key: key, size: 22, color: tokens.gold, semanticLabel: label)
            : AppIcon(
                AppIcons.seal,
                key: key,
                color: tokens.inkMuted.withValues(alpha: 0.6),
                size: 22,
                semanticLabel: label,
              ),
      ),
    );
  }
}
