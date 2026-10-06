import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';
import '../../../../core/auth/auth_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../characters/data/characters_controller.dart';
import '../../../characters/data/models.dart';
import '../../../characters/domain/class_theme.dart';
import '../../../characters/ui/character_avatar.dart';
import '../../../characters/ui/character_tabs.dart';
import '../../../characters/ui/combat/attacks_section.dart';
import '../../../characters/ui/combat/class_panels.dart';
import '../../../characters/ui/combat/resources_section.dart';
import '../../../characters/ui/combat/rest_section.dart';
import '../../../characters/ui/combat/vitals_section.dart';
import '../../../items/data/items_controllers.dart';
import '../../../items/ui/inventory_tab.dart';
import '../stash_card.dart';
import 'combat_items_section.dart';
import 'messages_inbox.dart';

/// The two halves of "Mi sesión".
enum PlayerSubview {
  combat('Combate'),
  outside('Fuera de combate');

  const PlayerSubview(this.label);

  final String label;
}

/// "Mi sesión": the view of a Player at the table. Finds their active
/// character in the campaign (with a selector when there are several) and
/// shows it split in "Combate" and "Fuera de combate". It never links to the
/// sheets of other players.
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

  /// Opens the guided creation wizard.
  void _create(BuildContext context) => context.push(AppRoutes.characterNew(campaign.id));

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
              onPressed: canWrite ? () => _create(context) : null,
              icon: const Icon(Icons.person_add_alt_1),
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

/// One character of the player in the selected [subview].
class _CharacterSession extends ConsumerWidget {
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
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(characterControllerProvider(characterId));
    return detail.when(
      skipLoadingOnReload: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => _ErrorView(
        message: describeCharacterError(error),
        onRetry: () => ref.invalidate(characterControllerProvider(characterId)),
      ),
      data: (character) => switch (subview) {
        PlayerSubview.combat => _CombatSubview(character: character),
        PlayerSubview.outside => _OutsideSubview(campaign: campaign, character: character),
      },
    );
  }
}

/// Bottom padding so the last card is not hidden behind the navigation bar.
EdgeInsets _listPadding(BuildContext context) =>
    EdgeInsets.fromLTRB(12, 4, 12, 32 + MediaQuery.paddingOf(context).bottom);

class _CombatSubview extends StatelessWidget {
  const _CombatSubview({required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    return ListView(
      key: const Key('player-combat'),
      padding: _listPadding(context),
      children: [
        _PlayerHeader(character: c),
        if (c.pendingLevelUpTo != null) _LevelUpCard(character: c),
        HpCard(character: c, canEdit: true),
        StatsCard(character: c, canEdit: true),
        if (c.hitPointsCurrent == 0) DeathSavesCard(character: c, canEdit: true),
        ConditionsCard(character: c, canEdit: true),
        AttacksSection(character: c),
        SpellSlotsSection(character: c, canEdit: true),
        ResourcesSection(character: c, canEdit: true),
        ClassPanelsSection(character: c, canEdit: true),
        CombatItemsSection(character: c, canEdit: true),
      ],
    );
  }
}

/// "¡Puedes subir a nivel N!": a DM granted the next level. Until the
/// level-up wizard exists its button opens the full sheet.
class _LevelUpCard extends StatelessWidget {
  const _LevelUpCard({required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final level = character.pendingLevelUpTo;
    return ParchmentCard(
      key: const Key('level-up-card'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          AppIcon(AppIcons.levelUp, size: 32, color: context.tokens.gold),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('¡Puedes subir a nivel $level!', style: theme.textTheme.titleMedium),
                Text('El DM te ha concedido un nivel.', style: theme.textTheme.bodySmall),
              ],
            ),
          ),
          const SizedBox(width: 8),
          FilledButton(
            key: const Key('level-up-open'),
            onPressed: () => context.push(AppRoutes.character(character.id)),
            child: const Text('Subir de nivel'),
          ),
        ],
      ),
    );
  }
}

/// Name, portrait and classes with the accent of the main class.
class _PlayerHeader extends StatelessWidget {
  const _PlayerHeader({required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    return ClassAccent(
      classIndex: mainClassIndex(c),
      child: Builder(
        builder: (context) {
          final theme = Theme.of(context);
          final main = mainClassIndex(c);
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
                      if (c.classes.isNotEmpty)
                        Row(
                          children: [
                            AppIcon(
                              classThemeOf(main).icon,
                              size: 18,
                              color: theme.colorScheme.primary,
                            ),
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                '${classesLabel(c.classes)} · Nivel ${c.totalLevel}',
                                style: theme.textTheme.bodyMedium,
                              ),
                            ),
                          ],
                        ),
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

class _OutsideSubview extends ConsumerWidget {
  const _OutsideSubview({required this.campaign, required this.character});

  final CampaignDetail campaign;
  final CharacterDetail character;

  void _open(BuildContext context, String title, Widget Function(CharacterDetail) builder) {
    Navigator.of(context, rootNavigator: true).push<void>(
      MaterialPageRoute(
        builder: (_) =>
            _CharacterSectionPage(title: title, characterId: character.id, builder: builder),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final tiles = <(String, String, IconData, Widget Function(CharacterDetail))>[
      (
        'spells',
        'Preparar hechizos',
        Icons.auto_stories_outlined,
        (ch) => SpellsTab(character: ch),
      ),
      ('traits', 'Rasgos', Icons.workspace_premium_outlined, (ch) => TraitsTab(character: ch)),
      ('notes', 'Trasfondo y notas', Icons.history_edu_outlined, (ch) => NotesTab(character: ch)),
      ('inventory', 'Inventario', Icons.backpack_outlined, (ch) => InventoryTab(character: ch)),
    ];
    return ListView(
      key: const Key('player-outside'),
      padding: _listPadding(context),
      children: [
        if (c.pendingLevelUpTo != null) _LevelUpCard(character: c),
        RestSection(character: c, canEdit: true),
        ParchmentCard(
          margin: const EdgeInsets.symmetric(vertical: 6),
          child: Column(
            children: [
              for (final (key, title, icon, builder) in tiles)
                ListTile(
                  key: Key('player-open-$key'),
                  leading: Icon(icon),
                  title: Text(title),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _open(context, title, builder),
                ),
              ListTile(
                key: const Key('player-open-sheet'),
                leading: const Icon(Icons.description_outlined),
                title: const Text('Hoja completa'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(AppRoutes.character(c.id)),
              ),
            ],
          ),
        ),
        _OpenShopsCard(campaignId: campaign.id),
        PartyStashCard(campaign: campaign, takerCharacterId: c.id),
        MessagesInbox(campaignId: campaign.id),
      ],
    );
  }
}

/// A part of the sheet of the player's character as a full page.
class _CharacterSectionPage extends ConsumerWidget {
  const _CharacterSectionPage({
    required this.title,
    required this.characterId,
    required this.builder,
  });

  final String title;
  final String characterId;
  final Widget Function(CharacterDetail character) builder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(characterControllerProvider(characterId));
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: detail.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(
          message: describeCharacterError(error),
          onRetry: () => ref.invalidate(characterControllerProvider(characterId)),
        ),
        data: builder,
      ),
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
                leading: const Icon(Icons.storefront_outlined),
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
