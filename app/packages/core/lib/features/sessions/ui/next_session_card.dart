import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../../../core/theme/textures.dart';
import '../../../core/theme/tokens.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../data/models.dart';
import '../data/sessions_controllers.dart';
import 'session_widgets.dart';

/// "Próxima sesión" card of the home page: the soonest upcoming session of any
/// of the user's campaigns, with the campaign, the local date and the answer of
/// the user. It takes to the session when tapped and stays hidden while there
/// is no session (or the request fails: the campaigns list is what matters).
///
/// The cached list may be stale, so the card skips sessions that already ended
/// on the local clock and, once the campaigns list is loaded, sessions of
/// campaigns that are no longer there (deleted or left).
class NextSessionCard extends ConsumerWidget {
  const NextSessionCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessions = ref.watch(mySessionsControllerProvider).value;
    if (sessions == null || sessions.isEmpty) return const SizedBox.shrink();
    final campaigns = ref.watch(campaignsControllerProvider).value;
    final s = pickNextSession(
      sessions,
      now: ref.watch(sessionsClockProvider)(),
      campaignIds: campaigns?.map((c) => c.id).toSet(),
    );
    if (s == null) return const SizedBox.shrink();
    final theme = Theme.of(context);

    return RuneCard(
      key: const Key('next-session-card'),
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      onTap: () => context.push(AppRoutes.session(s.campaignId, s.id)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            AppIcon(AppIcons.calendar, color: context.tokens.gold),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Próxima sesión', style: theme.textTheme.labelMedium),
                  Text(
                    '${s.campaignName} · ${sessionHeading(s.number, s.title)}',
                    key: const Key('next-session-title'),
                    style: theme.textTheme.titleSmall,
                  ),
                  Text(sessionWhen(s), key: const Key('next-session-when')),
                  const SizedBox(height: 2),
                  KeyedSubtree(
                    key: const Key('next-session-rsvp'),
                    child: MyRsvpLabel(rsvp: s.myRsvp),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

/// First session of [sessions] (sorted soonest first) that has not ended at
/// [now] and, when [campaignIds] is known, belongs to one of those campaigns.
Session? pickNextSession(
  List<Session> sessions, {
  required DateTime now,
  Set<String>? campaignIds,
}) {
  for (final s in sessions) {
    if (s.hasEnded(now)) continue;
    if (campaignIds != null && !campaignIds.contains(s.campaignId)) continue;
    return s;
  }
  return null;
}
