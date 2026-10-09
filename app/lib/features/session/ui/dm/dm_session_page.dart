import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../campaigns/ui/confirm_dialog.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../characters/data/characters_controller.dart';
import '../../../characters/data/models.dart' show RestKind, RestRequest;
import '../../../characters/ui/combat/rest_celebration.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../../items/data/items_controllers.dart';
import '../../../items/data/models.dart' show ShopSummary;
import '../../../sessions/ui/next_session_card.dart';
import '../../data/models.dart';
import '../../data/session_controllers.dart';
import '../session_feedback.dart';
import '../stash_card.dart';
import 'dm_character_sheet.dart';
import 'message_composer.dart';
import 'party_roster.dart';

/// "Mesa del DM": the party at a glance with forced rests, level grants, secret
/// messages and dice, the party stash, the shops with their open/closed switch,
/// the petitions (rest requests and change requests) and the next session. For
/// DMs and the Owner.
class DmSessionPage extends ConsumerStatefulWidget {
  const DmSessionPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<DmSessionPage> createState() => _DmSessionPageState();
}

class _DmSessionPageState extends ConsumerState<DmSessionPage> {
  /// Characters selected in the roster: rests and messages apply to them.
  final Set<String> _selected = {};

  String get _campaignId => widget.campaignId;

  void _toggle(String id) =>
      setState(() => _selected.contains(id) ? _selected.remove(id) : _selected.add(id));

  List<TableCharacter> _characters(List<PartyMember> party) => [
    for (final m in party) (id: m.id, name: m.name),
  ];

  /// Who an action applies to: the whole party or the selection.
  String _who(List<String> ids, List<PartyMember> party) => ids.isEmpty
      ? 'todo el grupo'
      : ids.length == 1
      ? party.firstWhere((m) => m.id == ids.single).name
      : '${ids.length} personajes';

  Future<void> _rest(PartyRestKind kind, List<PartyMember> party) async {
    final ids = _selected.where((id) => party.any((m) => m.id == id)).toList();
    final who = _who(ids, party);
    final confirmed = await confirmAction(
      context,
      title: kind.label,
      message: '¿Declarar un ${kind.label.toLowerCase()} para $who?',
      confirmLabel: 'Descansar',
    );
    if (!confirmed || !mounted) return;
    final done = await runTableAction(
      context,
      () => ref
          .read(partyControllerProvider(_campaignId).notifier)
          .rest(kind, characterIds: ids.isEmpty ? null : ids),
      success: '${kind.label} aplicado a $who.',
    );
    if (done && mounted) setState(_selected.clear);
  }

  Future<void> _grantLevel(List<PartyMember> party) async {
    final ids = _selected.where((id) => party.any((m) => m.id == id)).toList();
    final who = _who(ids, party);
    final confirmed = await confirmAction(
      context,
      title: 'Conceder nivel',
      message:
          '¿Conceder el siguiente nivel a $who? Cada jugador lo elegirá desde su sesión; '
          'quien ya tenga un nivel pendiente no acumula otro.',
      confirmLabel: 'Conceder',
    );
    if (!confirmed || !mounted) return;
    final done = await runTableAction(
      context,
      () => ref
          .read(partyControllerProvider(_campaignId).notifier)
          .grantLevel(characterIds: ids.isEmpty ? null : ids),
      success: 'Nivel concedido a $who.',
    );
    if (done && mounted) setState(_selected.clear);
  }

  Future<void> _message(List<PartyMember> party) async {
    final message = await showMessageComposer(
      context,
      characters: _characters(party),
      initial: _selected,
    );
    if (message == null || !mounted) return;
    await runTableAction(
      context,
      () => ref
          .read(messagesControllerProvider(_campaignId).notifier)
          .send(characterIds: message.characterIds, body: message.body),
      success: message.characterIds.length == 1 ? 'Mensaje enviado.' : 'Mensajes enviados.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final campaign = ref.watch(campaignDetailControllerProvider(_campaignId)).value;
    if (campaign == null) return const Center(child: CircularProgressIndicator());
    final partyAsync = ref.watch(partyControllerProvider(_campaignId));
    final party = partyAsync.value ?? const <PartyMember>[];
    final conditions = ref.watch(conditionsProvider).value ?? const [];

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(partyControllerProvider(_campaignId));
        ref.invalidate(restRequestsControllerProvider(_campaignId));
        ref.invalidate(stashControllerProvider(_campaignId));
        ref.invalidate(shopsControllerProvider(_campaignId));
      },
      child: ListView(
        key: const Key('dm-session'),
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 32),
        children: [
          _ActionBar(
            selectedCount: _selected.length,
            onShortRest: () => _rest(PartyRestKind.short, party),
            onLongRest: () => _rest(PartyRestKind.long, party),
            onGrantLevel: () => _grantLevel(party),
            onMessage: () => _message(party),
            onClearSelection: () => setState(_selected.clear),
          ),
          const SectionHeader('Grupo'),
          partyAsync.when(
            skipLoadingOnReload: true,
            loading: () => const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => Column(
              children: [
                Text(describeTableError(error), textAlign: TextAlign.center),
                TextButton(
                  onPressed: () => ref.invalidate(partyControllerProvider(_campaignId)),
                  child: const Text('Reintentar'),
                ),
              ],
            ),
            data: (members) => PartyRoster(
              members: members,
              selected: _selected,
              conditions: conditions,
              onToggle: _toggle,
              onOpen: (m) =>
                  showDmCharacterSheet(context, campaignId: _campaignId, characterId: m.id),
            ),
          ),
          const SizedBox(height: 8),
          PartyStashCard(campaign: campaign, characters: _characters(party)),
          _ShopsCard(campaign: campaign),
          _PetitionsCard(campaignId: _campaignId),
          const NextSessionCard(),
        ],
      ),
    );
  }
}

class _ActionBar extends StatelessWidget {
  const _ActionBar({
    required this.selectedCount,
    required this.onShortRest,
    required this.onLongRest,
    required this.onGrantLevel,
    required this.onMessage,
    required this.onClearSelection,
  });

  final int selectedCount;
  final VoidCallback onShortRest;
  final VoidCallback onLongRest;
  final VoidCallback onGrantLevel;
  final VoidCallback onMessage;
  final VoidCallback onClearSelection;

  @override
  Widget build(BuildContext context) {
    final suffix = selectedCount == 0 ? '' : ' ($selectedCount)';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _ActionGrid(
          key: const Key('dm-action-grid'),
          children: [
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-short-rest'),
                onPressed: canWrite ? onShortRest : null,
                icon: AppIcons.campfire,
                label: 'Descanso corto$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-long-rest'),
                onPressed: canWrite ? onLongRest : null,
                icon: AppIcons.moon,
                label: 'Descanso largo$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('party-grant-level'),
                onPressed: canWrite ? onGrantLevel : null,
                icon: AppIcons.levelUp,
                label: 'Conceder nivel$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-message'),
                onPressed: canWrite ? onMessage : null,
                icon: AppIcons.envelope,
                label: 'Mensaje secreto',
              ),
            ),
            _ActionButton(
              key: const Key('dm-dice'),
              onPressed: () => showDiceSheet(context),
              icon: AppIcons.d20,
              label: 'Dados',
            ),
          ],
        ),
        if (selectedCount > 0)
          TextButton.icon(
            key: const Key('dm-clear-selection'),
            onPressed: onClearSelection,
            icon: const Icon(Icons.deselect),
            label: Text(
              selectedCount == 1
                  ? '1 seleccionado · Quitar selección'
                  : '$selectedCount seleccionados · Quitar selección',
            ),
          ),
      ],
    );
  }
}

/// Equal boxes for the table's actions: two columns on a phone, one row of
/// [children].length from 600 px wide. Every box has the same width and
/// height whatever its label, so the row reads as one toolbar.
class _ActionGrid extends StatelessWidget {
  const _ActionGrid({super.key, required this.children});

  final List<Widget> children;

  static const _spacing = 8.0;
  static const _height = 44.0;
  static const _wideBreakpoint = 600.0;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth >= _wideBreakpoint ? children.length : 2;
        final width = (constraints.maxWidth - _spacing * (columns - 1)) / columns;
        return Wrap(
          spacing: _spacing,
          runSpacing: _spacing,
          children: [
            for (final child in children) SizedBox(width: width, height: _height, child: child),
          ],
        );
      },
    );
  }
}

/// A tonal button of the [_ActionGrid]; the label shrinks instead of
/// overflowing when the box is narrow.
class _ActionButton extends StatelessWidget {
  const _ActionButton({
    super.key,
    required this.onPressed,
    required this.icon,
    required this.label,
  });

  final VoidCallback? onPressed;
  final AppIcons icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return FilledButton.tonalIcon(
      onPressed: onPressed,
      style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 12)),
      icon: AppIcon(icon, size: 20),
      label: FittedBox(fit: BoxFit.scaleDown, child: Text(label, maxLines: 1)),
    );
  }
}

/// Shops of the campaign with their open/closed switch.
class _ShopsCard extends ConsumerWidget {
  const _ShopsCard({required this.campaign});

  final CampaignDetail campaign;

  Future<void> _toggle(BuildContext context, WidgetRef ref, ShopSummary shop, bool open) =>
      runTableAction(
        context,
        () => ref.read(shopsControllerProvider(campaign.id).notifier).setOpen(shop.id, open),
        success: open ? '${shop.name} está abierta.' : '${shop.name} está cerrada.',
      );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final shops = ref.watch(shopsControllerProvider(campaign.id));
    return ParchmentCard(
      key: const Key('dm-shops'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              AppIcon(AppIcons.coins, color: context.tokens.gold),
              const SizedBox(width: 8),
              Expanded(child: Text('Tiendas', style: theme.textTheme.titleMedium)),
            ],
          ),
          shops.when(
            skipLoadingOnReload: true,
            loading: () => const Padding(
              padding: EdgeInsets.all(12),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => TextButton(
              onPressed: () => ref.invalidate(shopsControllerProvider(campaign.id)),
              child: const Text('No se pudieron cargar las tiendas. Reintentar'),
            ),
            data: (list) => list.isEmpty
                ? const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('Aún no hay tiendas en esta campaña.'),
                  )
                : Column(
                    children: [
                      for (final shop in list)
                        OfflineAware(
                          builder: (context, canWrite) => SwitchListTile(
                            key: Key('dm-shop-${shop.id}'),
                            contentPadding: EdgeInsets.zero,
                            title: Text(shop.name),
                            subtitle: Text(shop.isOpen ? 'Abierta' : 'Cerrada'),
                            value: shop.isOpen,
                            onChanged: canWrite
                                ? (open) => _toggle(context, ref, shop, open)
                                : null,
                            secondary: IconButton(
                              key: Key('dm-shop-open-${shop.id}'),
                              tooltip: 'Abrir tienda',
                              onPressed: () => context.push(AppRoutes.shop(campaign.id, shop.id)),
                              icon: const AppIcon(AppIcons.treasure),
                            ),
                          ),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

/// "Peticiones": the rest requests of the players with Aprobar / Rechazar in
/// the row itself, and the count of pending change requests with a link to
/// the list. Approving a rest plays its campfire or moon over the screen.
class _PetitionsCard extends ConsumerWidget {
  const _PetitionsCard({required this.campaignId});

  final String campaignId;

  Future<void> _approve(BuildContext context, WidgetRef ref, RestRequest request) async {
    final done = await runTableAction(
      context,
      () => ref.read(restRequestsControllerProvider(campaignId).notifier).approve(request),
      success: 'Descanso aprobado para ${request.characterName}.',
    );
    if (done && context.mounted) showRestCelebration(context, request.kind);
  }

  Future<void> _reject(BuildContext context, WidgetRef ref, RestRequest request) => runTableAction(
    context,
    () => ref.read(restRequestsControllerProvider(campaignId).notifier).reject(request),
    success: 'Descanso rechazado para ${request.characterName}.',
  );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final pending = ref.watch(pendingChangeRequestCountProvider(campaignId));
    final rests = ref.watch(restRequestsControllerProvider(campaignId));
    return ParchmentCard(
      key: const Key('dm-petitions'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              AppIcon(AppIcons.scroll, color: context.tokens.gold),
              const SizedBox(width: 8),
              Expanded(child: Text('Peticiones', style: theme.textTheme.titleMedium)),
            ],
          ),
          rests.when(
            skipLoadingOnReload: true,
            loading: () => const Padding(
              padding: EdgeInsets.all(12),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => TextButton(
              onPressed: () => ref.invalidate(restRequestsControllerProvider(campaignId)),
              child: const Text('No se pudieron cargar las peticiones de descanso. Reintentar'),
            ),
            data: (list) => list.isEmpty
                ? const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                      'No hay descansos pendientes de aprobar.',
                      key: Key('dm-petitions-empty'),
                    ),
                  )
                : Column(
                    children: [
                      for (final request in list)
                        _RestPetitionRow(
                          request: request,
                          onApprove: () => _approve(context, ref, request),
                          onReject: () => _reject(context, ref, request),
                        ),
                    ],
                  ),
          ),
          const Divider(height: 1),
          ListTile(
            key: const Key('dm-change-requests'),
            contentPadding: EdgeInsets.zero,
            leading: Badge(
              isLabelVisible: pending > 0,
              label: Text('$pending', style: AppTypography.numeric),
              child: AppIcon(AppIcons.quill, color: context.tokens.gold),
            ),
            title: const Text('Solicitudes pendientes'),
            subtitle: Text(
              pending == 0
                  ? 'No hay solicitudes pendientes.'
                  : pending == 1
                  ? '1 solicitud por revisar'
                  : '$pending solicitudes por revisar',
              key: const Key('dm-change-requests-count'),
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push(AppRoutes.changeRequests(campaignId)),
          ),
        ],
      ),
    );
  }
}

/// A pending rest request: who, which rest, how many dice and since when, with
/// its Aprobar and Rechazar buttons.
class _RestPetitionRow extends StatelessWidget {
  const _RestPetitionRow({required this.request, required this.onApprove, required this.onReject});

  final RestRequest request;
  final VoidCallback onApprove;
  final VoidCallback onReject;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final at = request.requestedAt;
    final age = at == null ? null : describeDataAge(at.toLocal(), DateTime.now());
    return Padding(
      key: Key('rest-petition-${request.id}'),
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          AppIcon(
            request.kind == RestKind.short ? AppIcons.campfire : AppIcons.moon,
            size: 22,
            color: context.tokens.gold,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(request.characterName, style: theme.textTheme.titleSmall),
                Text([request.description, ?age].join(' · '), style: theme.textTheme.bodySmall),
              ],
            ),
          ),
          OfflineAware(
            builder: (context, canWrite) => TextButton(
              key: Key('rest-reject-${request.id}'),
              onPressed: canWrite ? onReject : null,
              child: const Text('Rechazar'),
            ),
          ),
          OfflineAware(
            builder: (context, canWrite) => FilledButton.tonal(
              key: Key('rest-approve-${request.id}'),
              onPressed: canWrite ? onApprove : null,
              child: const Text('Aprobar'),
            ),
          ),
        ],
      ),
    );
  }
}
