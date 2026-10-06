import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';
import 'new_character_dialog.dart';

/// "Personajes" section of a campaign: summary cards plus the "new character"
/// button. A player opens only their own characters; the others show name,
/// class and level without a link (DMs open every one).
class CharactersTab extends ConsumerWidget {
  const CharactersTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  Future<void> _create(BuildContext context, WidgetRef ref, String myUserId) async {
    final data = await showDialog<NewCharacterData>(
      context: context,
      builder: (_) => NewCharacterDialog(
        members: campaign.members,
        myUserId: myUserId,
        canChooseOwner: campaign.myRole.isAtLeastDm,
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () async {
        await ref
            .read(campaignCharactersControllerProvider(campaign.id).notifier)
            .create(data.name, owner: data.owner);
      },
      success: 'Personaje creado.',
      describe: describeCharacterError,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final characters = ref.watch(campaignCharactersControllerProvider(campaign.id));

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: OfflineAwareFab(
        fabKey: const Key('characters-new'),
        onPressed: () => _create(context, ref, myUserId),
        icon: const Icon(Icons.person_add_alt_1),
        label: const Text('Nuevo personaje'),
      ),
      body: characters.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeCharacterError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () =>
                      ref.invalidate(campaignCharactersControllerProvider(campaign.id)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
        data: (list) => RefreshIndicator(
          onRefresh: () =>
              ref.read(campaignCharactersControllerProvider(campaign.id).notifier).reload(),
          child: list.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: const [
                    SizedBox(height: 96),
                    Center(child: Text('Aún no hay personajes en esta campaña')),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.only(top: 8, bottom: 96),
                  children: [
                    for (final c in list)
                      CharacterCard(
                        character: c,
                        canOpen: campaign.myRole.isAtLeastDm || c.ownerUserId == myUserId,
                      ),
                  ],
                ),
        ),
      ),
    );
  }
}

/// Summary card: name, race, classes and level, status chip and HP when visible.
/// Without [canOpen] (someone else's character for a player) it has no link
/// and no hit points.
class CharacterCard extends StatelessWidget {
  const CharacterCard({super.key, required this.character, this.canOpen = true});

  final CharacterSummary character;
  final bool canOpen;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = character;
    final classes = c.classes.isEmpty ? 'Sin clase' : classesLabel(c.classes);
    final subtitle = [
      if (c.raceName != null) c.raceName!,
      classes,
      if (c.classes.isNotEmpty) 'Nivel ${c.level}',
    ].join(' · ');
    final hp = c.hitPointsMax == null || !canOpen
        ? null
        : 'PG ${c.hitPointsCurrent ?? c.hitPointsMax} / ${c.hitPointsMax}';

    return Card(
      key: Key('character-${c.id}'),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: canOpen ? () => context.push(AppRoutes.character(c.id)) : null,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(c.name, style: theme.textTheme.titleMedium)),
                  const SizedBox(width: 8),
                  Chip(
                    label: Text(c.status.label),
                    visualDensity: VisualDensity.compact,
                    padding: EdgeInsets.zero,
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Text(subtitle, style: theme.textTheme.bodyMedium),
              const SizedBox(height: 4),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      c.ownerUserId == null
                          ? 'PNJ'
                          : 'Jugador: ${c.ownerDisplayName ?? 'Desconocido'}',
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
                  if (hp != null) Text(hp, style: theme.textTheme.labelLarge),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
