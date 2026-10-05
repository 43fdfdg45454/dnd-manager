import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../data/sessions_controllers.dart';
import 'session_widgets.dart';

/// "Próxima sesión" card of the home page: the soonest upcoming session of any
/// of the user's campaigns, with the campaign, the local date and the answer of
/// the user. It takes to the session when tapped and stays hidden while there
/// is no session (or the request fails: the campaigns list is what matters).
class NextSessionCard extends ConsumerWidget {
  const NextSessionCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessions = ref.watch(mySessionsControllerProvider).value;
    if (sessions == null || sessions.isEmpty) return const SizedBox.shrink();
    final s = sessions.first;
    final theme = Theme.of(context);

    return Card(
      key: const Key('next-session-card'),
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.push(AppRoutes.session(s.campaignId, s.id)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Icon(Icons.event, color: theme.colorScheme.primary),
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
      ),
    );
  }
}
