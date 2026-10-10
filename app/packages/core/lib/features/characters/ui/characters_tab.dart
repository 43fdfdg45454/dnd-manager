import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/api_error.dart';
import '../../../core/network/connectivity.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/characters_controller.dart';
import '../../../core/characters/models.dart';
import '../../../core/systems/system_registry.dart';
import 'new_character_dialog.dart';

/// "Personajes" section of a campaign: summary cards plus the "new character"
/// button. A player opens only their own characters; the others show name,
/// class and level without a link (DMs open every one).
class CharactersTab extends ConsumerWidget {
  const CharactersTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  /// Opens the guided creation wizard of the game system.
  void _openWizard(BuildContext context, WidgetRef ref) =>
      ref.read(campaignSystemUiProvider(campaign.id)).openCreationWizard(context, campaign.id);

  /// DM shortcut: name and player only (an NPC by default).
  Future<void> _createQuick(BuildContext context, WidgetRef ref, String myUserId) async {
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
      floatingActionButton: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (campaign.myRole.isAtLeastDm) ...[
            _QuickCreateMenu(onSelected: () => _createQuick(context, ref, myUserId)),
            const SizedBox(width: 12),
          ],
          OfflineAwareFab(
            fabKey: const Key('characters-new'),
            onPressed: () => _openWizard(context, ref),
            icon: const Icon(Icons.person_add_alt_1),
            label: const Text('Nuevo personaje'),
          ),
        ],
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

/// Secondary menu of the DM next to "Nuevo personaje": quick NPC creation.
class _QuickCreateMenu extends ConsumerWidget {
  const _QuickCreateMenu({required this.onSelected});

  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canWrite = ref.watch(canWriteProvider);
    return PopupMenuButton<String>(
      key: const Key('characters-more'),
      enabled: canWrite,
      tooltip: canWrite ? 'Más opciones' : needsConnectionMessage,
      onSelected: (_) => onSelected(),
      itemBuilder: (context) => const [
        PopupMenuItem<String>(
          key: Key('characters-quick'),
          value: 'quick',
          child: Text('Crear rápido (PNJ)'),
        ),
      ],
    );
  }
}

/// Summary card: name, the roster line of the game system (D&D 5e: race,
/// classes and level), status chip and its status (D&D 5e: HP) when visible.
/// Without [canOpen] (someone else's character for a player) it has no link
/// and no status.
class CharacterCard extends ConsumerWidget {
  const CharacterCard({super.key, required this.character, this.canOpen = true});

  final CharacterSummary character;
  final bool canOpen;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = character;
    final system = ref.watch(campaignSystemUiProvider(c.campaignId));
    final subtitle = system.rosterSubtitle(c);
    final hp = canOpen ? system.rosterStatus(c) : null;

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
                  if (c.ownerUserId == null) ...[
                    const SizedBox(width: 8),
                    Chip(
                      key: Key('character-npc-${c.id}'),
                      label: const Text('PNJ'),
                      visualDensity: VisualDensity.compact,
                      padding: EdgeInsets.zero,
                      backgroundColor: theme.colorScheme.tertiaryContainer,
                      labelStyle: TextStyle(color: theme.colorScheme.onTertiaryContainer),
                    ),
                  ],
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
                          ? 'Sin jugador'
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
