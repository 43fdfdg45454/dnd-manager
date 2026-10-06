import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../cache/stale_data.dart';
import '../../features/campaigns/data/campaigns_repository.dart';
import '../ui/offline_widgets.dart';
import 'realtime_hub.dart';
import 'realtime_provider.dart';

/// Strip under the campaign app bar while the realtime connection is not up:
/// amber "Reconectando… (N s)" while a (re)connection is under way or the next
/// attempt is scheduled, red "Sin conexión en vivo · datos de hace X ·
/// Reintentar" otherwise. Invisible when connected.
class ConnectionBanner extends ConsumerWidget {
  const ConnectionBanner({super.key, required this.campaignId, this.now = DateTime.now});

  final String campaignId;

  /// Clock (tests).
  final DateTime Function() now;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(campaignRealtimeProvider(campaignId));
    if (state.status == RealtimeStatus.connected) return const SizedBox.shrink();

    Widget retryButton(Color color) => TextButton(
      key: const Key('realtime-retry'),
      style: TextButton.styleFrom(foregroundColor: color),
      onPressed: () => ref.read(campaignRealtimeProvider(campaignId).notifier).retryNow(),
      child: const Text('Reintentar'),
    );

    final retrying =
        state.status == RealtimeStatus.connecting ||
        state.status == RealtimeStatus.reconnecting ||
        (state.status == RealtimeStatus.disconnected && state.nextRetryAt != null);
    if (retrying) {
      return _Strip(
        key: const Key('connection-banner-reconnecting'),
        amber: true,
        builder: (context) {
          final next = state.nextRetryAt;
          final seconds = next == null
              ? null
              : (next.difference(now()).inMilliseconds / 1000).ceil();
          final countdown = seconds == null ? '' : ' (${seconds < 0 ? 0 : seconds} s)';
          return [
            Expanded(child: Text('Reconectando…$countdown')),
            if (next != null) retryButton(Colors.black87),
          ];
        },
        ticking: state.nextRetryAt != null,
      );
    }

    final stale = ref.watch(
      staleSinceProvider(staleTree(CampaignsRepository.campaignPath(campaignId))),
    );
    final since = stale ?? state.lastConnectedAt;
    final age = since == null ? null : describeDataAge(since.toLocal(), now());
    final text = age == null ? 'Sin conexión en vivo' : 'Sin conexión en vivo · datos de $age';
    return _Strip(
      key: const Key('connection-banner-offline'),
      amber: false,
      ticking: false,
      builder: (context) => [Expanded(child: Text(text)), retryButton(Colors.white)],
    );
  }
}

/// Coloured strip; rebuilds every second when [ticking] (the countdown).
class _Strip extends StatefulWidget {
  const _Strip({super.key, required this.amber, required this.builder, required this.ticking});

  final bool amber;
  final bool ticking;
  final List<Widget> Function(BuildContext context) builder;

  @override
  State<_Strip> createState() => _StripState();
}

class _StripState extends State<_Strip> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _syncTimer();
  }

  @override
  void didUpdateWidget(_Strip oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncTimer();
  }

  void _syncTimer() {
    if (widget.ticking && _timer == null) {
      _timer = Timer.periodic(const Duration(seconds: 1), (_) {
        if (mounted) setState(() {});
      });
    } else if (!widget.ticking) {
      _timer?.cancel();
      _timer = null;
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final background = widget.amber ? Colors.amber.shade700 : Colors.red.shade700;
    final foreground = widget.amber ? Colors.black87 : Colors.white;
    return Material(
      color: background,
      child: DefaultTextStyle.merge(
        style: Theme.of(context).textTheme.bodySmall?.copyWith(color: foreground),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
          child: Row(
            children: [
              Icon(
                widget.amber ? Icons.sync : Icons.cloud_off_outlined,
                size: 18,
                color: foreground,
              ),
              const SizedBox(width: 8),
              ...widget.builder(context),
            ],
          ),
        ),
      ),
    );
  }
}
