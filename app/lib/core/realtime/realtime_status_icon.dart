import 'package:flutter/widgets.dart';

/// Slot of the real-time connection status in the app bar of a campaign
/// (`Key('realtime-status')`). Phase 13d replaces this placeholder with the
/// connected / reconnecting / offline icon of the campaign hub.
class RealtimeStatusIcon extends StatelessWidget {
  const RealtimeStatusIcon({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context) => const SizedBox.shrink(key: Key('realtime-status'));
}
