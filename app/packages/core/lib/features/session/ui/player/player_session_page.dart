import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';
import '../../../../core/auth/auth_state.dart';
import '../../../../core/characters/models.dart';
import '../../../../core/motion/vignette.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/systems/game_system_ui.dart';
import '../../../../core/systems/system_registry.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../characters/data/characters_controller.dart';
import '../../../characters/domain/character_permissions.dart';
import '../../../characters/ui/character_avatar.dart';
import '../../../characters/ui/character_detail_tabs.dart';
import '../../../items/data/items_controllers.dart';
import '../stash_card.dart';
import 'messages_inbox.dart';

/// The two halves of "Mi sesión", the same main views as the character page.
enum PlayerSubview {
  combat('Combate'),
  detail('Detalle');

  const PlayerSubview(this.label);

  final String label;
}

/// "Mi sesión": the view of a Player at the table. Finds their active
/// character in the campaign (with a selector when there are several) and
/// shows it split in "Combate" and "Detalle" (the sub-tabs of the sheet with a
/// first "Sesión" sub-tab). It never links to the sheets of other players.
class PlayerSessionPage extends ConsumerStatefulWidget {
  const PlayerSessionPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<PlayerSessionPage> createState() => _PlayerSessionPageState();
}

class _PlayerSessionPageState extends ConsumerState<PlayerSessionPage> {
  String? _selectedId;
  PlayerSubview _subview = PlayerSubview.combat;

  @override
  Widget build(BuildContext context) {
    final campaign = ref.watch(campaignDetailControllerProvider(widget.campaignId)).value;
    if (campaign == null) return const Center(child: CircularProgressIndicator());
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final characters = ref.watch(campaignCharactersControllerProvider(widget.campaignId));

    return characters.when(
      skipLoadingOnReload: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => _ErrorView(
        message: describeCharacterError(error),
        onRetry: () => ref.invalidate(campaignCharactersControllerProvider(widget.campaignId)),
      ),
      data: (list) {
        final mine = [
          for (final c in list)
            if (c.ownerUserId != null && c.ownerUserId == myUserId) c,
        ];
        final active = [
          for (final c in mine)
            if (c.status == CharacterStatus.active) c,
        ];
        if (active.isEmpty) {
          return _NoCharacter(campaign: campaign, drafts: mine);
        }
        final selected = active.firstWhere((c) => c.id == _selectedId, orElse: () => active.first);
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (active.length > 1)
                    DropdownButtonFormField<String>(
                      key: const Key('player-character-select'),
                      initialValue: selected.id,
                      decoration: const InputDecoration(
                        labelText: 'Personaje',
                        isDense: true,
                        border: OutlineInputBorder(),
                      ),
                      items: [
                        for (final c in active) DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      onChanged: (id) => setState(() => _selectedId = id),
                    ),
                  if (active.length > 1) const SizedBox(height: 8),
                  SegmentedButton<PlayerSubview>(
                    key: const Key('player-subview'),
                    showSelectedIcon: false,
                    segments: [
                      for (final v in PlayerSubview.values)
                        ButtonSegment(
                          value: v,
                          label: Text(v.label, key: Key('player-subview-${v.name}')),
                        ),
                    ],
                    selected: {_subview},
                    onSelectionChanged: (s) => setState(() => _subview = s.first),
                  ),
                ],
              ),
            ),
            Expanded(
              child: _CharacterSession(
                key: ValueKey(selected.id),
                campaign: campaign,
                characterId: selected.id,
                subview: _subview,
              ),
            ),
          ],
        );
      },
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('Reintentar'),
          ),
        ],
      ),
    ),
  );
}

/// The player has no active character: a notice, the button to create one and
/// the drafts in progress.
class _NoCharacter extends ConsumerWidget {
  const _NoCharacter({required this.campaign, required this.drafts});

  final CampaignDetail campaign;
  final List<CharacterSummary> drafts;

  /// Opens the guided creation wizard of the game system.
  void _create(BuildContext context, WidgetRef ref) =>
      ref.read(campaignSystemUiProvider(campaign.id)).openCreationWizard(context, campaign.id);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return ListView(
      key: const Key('player-no-character'),
      padding: const EdgeInsets.all(24),
      children: [
        AppIcon(AppIcons.hood, size: 56, color: context.tokens.gold),
        const SizedBox(height: 12),
        Text(
          'No tienes personaje en esta campaña',
          textAlign: TextAlign.center,
          style: theme.textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        Text(
          drafts.isEmpty
              ? 'Crea uno y envíalo al DM para que lo active.'
              : 'Tus borradores aparecerán aquí cuando el DM los active.',
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        Center(
          child: OfflineAware(
            builder: (context, canWrite) => FilledButton.icon(
              key: const Key('player-create-character'),
              onPressed: canWrite ? () => _create(context, ref) : null,
              icon: const AppIcon(AppIcons.hood, size: 20),
              label: const Text('Crear personaje'),
            ),
          ),
        ),
        if (drafts.isNotEmpty) ...[
          const SectionHeader('Borradores'),
          for (final d in drafts)
            ListTile(
              key: Key('player-draft-${d.id}'),
              title: Text(d.name),
              subtitle: Text(d.status.label),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push(AppRoutes.character(d.id)),
            ),
        ],
      ],
    );
  }
}

/// One character of the player in the selected [subview]. While the game
/// system has a full-screen page the player must complete
/// ([GameSystemUi.pendingActionRoute]; D&D 5e: the level-up wizard,
/// "Sustituye lo que ya no cumples", "Prepara tus conjuros" and "Tira tus
/// dados", in this order), it opens by itself and cannot be left (also when a
/// `character.updated` event activates it).
class _CharacterSession extends ConsumerStatefulWidget {
  const _CharacterSession({
    super.key,
    required this.campaign,
    required this.characterId,
    required this.subview,
  });

  final CampaignDetail campaign;
  final String characterId;
  final PlayerSubview subview;

  @override
  ConsumerState<_CharacterSession> createState() => _CharacterSessionState();
}

class _CharacterSessionState extends ConsumerState<_CharacterSession> {
  /// A forced page is open (or about to open).
  bool _forcing = false;

  /// Opens the forced page that [character] needs, if any (one at a time).
  void _force(CharacterDetail character) {
    if (_forcing) return;
    final target = ref
        .read(campaignSystemUiProvider(widget.campaign.id))
        .pendingActionRoute(character);
    if (target == null) return;
    _forcing = true;
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      if (!mounted) {
        _forcing = false;
        return;
      }
      await context.push<void>(target);
      if (!mounted) return;
      // Fresh state decides whether the next forced page is needed.
      try {
        await ref.read(characterControllerProvider(widget.characterId).notifier).reload();
      } catch (_) {
        // Offline or failed: the cached state stays; do not loop.
        _forcing = false;
        return;
      }
      if (mounted) setState(() => _forcing = false);
    });
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(characterControllerProvider(widget.characterId));
    final system = ref.watch(campaignSystemUiProvider(widget.campaign.id));
    final loaded = detail.value;
    if (loaded != null && loaded.status == CharacterStatus.active) _force(loaded);
    return detail.when(
      skipLoadingOnReload: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => _ErrorView(
        message: describeCharacterError(error),
        onRetry: () => ref.invalidate(characterControllerProvider(widget.characterId)),
      ),
      // A character down (D&D 5e: at 0 hit points) darkens the edges of the
      // session.
      data: (character) => DarkVignette(
        key: const Key('player-vignette'),
        active: system.isDown(character),
        child: switch (widget.subview) {
          PlayerSubview.combat => system.combatView(
            context,
            character,
            canEdit: true,
            inSession: true,
            header: _PlayerHeader(system: system, character: character),
          ),
          PlayerSubview.detail => _DetailSubview(
            system: system,
            campaign: widget.campaign,
            character: character,
          ),
        },
      ),
    );
  }
}

/// Bottom padding so the last card is not hidden behind the navigation bar.
EdgeInsets _listPadding(BuildContext context) =>
    EdgeInsets.fromLTRB(12, 4, 12, 32 + MediaQuery.paddingOf(context).bottom);

/// Name, portrait and the short line of the game system with the accent of
/// the character (D&D 5e: classes and level, with the colour of the main
/// class).
class _PlayerHeader extends StatelessWidget {
  const _PlayerHeader({required this.system, required this.character});

  final GameSystemUi system;
  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    return system.characterAccent(
      c,
      child: Builder(
        builder: (context) {
          final theme = Theme.of(context);
          final headline = system.characterHeadline(c, compact: true);
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Row(
              children: [
                CharacterAvatar(character: c, radius: 24),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        c.name,
                        key: const Key('player-character-name'),
                        style: theme.textTheme.headlineSmall,
                      ),
                      ?headline,
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

/// "Detalle": the player's header over the sub-tabs of the sheet
/// ([CharacterDetailTabs]) with a first "Sesión" sub-tab: the cards of the
/// game system (D&D 5e: the level granted and the rests), the open shops, the
/// party stash and the DM's messages.
class _DetailSubview extends ConsumerWidget {
  const _DetailSubview({required this.system, required this.campaign, required this.character});

  final GameSystemUi system;
  final CampaignDetail campaign;
  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final auth = ref.watch(authControllerProvider);
    final permissions = CharacterPermissions(
      character: c,
      myUserId: auth is AuthSignedIn ? auth.user.id : '',
      myRole: campaign.myRole,
    );
    return Column(
      key: const Key('player-detail'),
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: _PlayerHeader(system: system, character: c),
        ),
        Expanded(
          child: CharacterDetailTabs(
            character: c,
            permissions: permissions,
            session: ListView(
              key: const Key('player-session'),
              padding: _listPadding(context),
              children: [
                ...system.sessionCards(context, c),
                _OpenShopsCard(campaignId: campaign.id),
                PartyStashCard(campaign: campaign, takerCharacterId: c.id),
                MessagesInbox(campaignId: campaign.id),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// The open shops of the campaign (players only get those).
class _OpenShopsCard extends ConsumerWidget {
  const _OpenShopsCard({required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final shops = ref.watch(shopsControllerProvider(campaignId));
    final open = [
      for (final s in shops.value ?? const [])
        if (s.isOpen) s,
    ];
    return ParchmentCard(
      key: const Key('player-shops'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              AppIcon(AppIcons.coins, color: context.tokens.gold),
              const SizedBox(width: 8),
              Text('Tiendas abiertas', style: theme.textTheme.titleMedium),
            ],
          ),
          if (shops.isLoading && shops.value == null)
            const Padding(
              padding: EdgeInsets.all(12),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (shops.hasError && shops.value == null)
            TextButton(
              onPressed: () => ref.invalidate(shopsControllerProvider(campaignId)),
              child: const Text('No se pudieron cargar las tiendas. Reintentar'),
            )
          else if (open.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('No hay tiendas abiertas.', key: Key('player-shops-empty')),
            )
          else
            for (final shop in open)
              ListTile(
                key: Key('player-shop-${shop.id}'),
                contentPadding: EdgeInsets.zero,
                leading: const AppIcon(AppIcons.treasure),
                title: Text(shop.name),
                subtitle: shop.description == null || shop.description!.isEmpty
                    ? null
                    : Text(shop.description!, maxLines: 1, overflow: TextOverflow.ellipsis),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(AppRoutes.shop(campaignId, shop.id)),
              ),
        ],
      ),
    );
  }
}
