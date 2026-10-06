import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../campaigns/ui/confirm_dialog.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../characters/data/characters_controller.dart';
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

/// "Mesa del DM": the party at a glance with forced rests, secret messages and
/// dice, the party stash, the shops with their open/closed switch, the pending
/// change requests and the next session. For DMs and the Owner.
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

  Future<void> _rest(PartyRestKind kind, List<PartyMember> party) async {
    final ids = _selected.where((id) => party.any((m) => m.id == id)).toList();
    final who = ids.isEmpty
        ? 'todo el grupo'
        : ids.length == 1
        ? party.firstWhere((m) => m.id == ids.single).name
        : '${ids.length} personajes';
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
          _ChangeRequestsCard(campaignId: _campaignId),
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
    required this.onMessage,
    required this.onClearSelection,
  });

  final int selectedCount;
  final VoidCallback onShortRest;
  final VoidCallback onLongRest;
  final VoidCallback onMessage;
  final VoidCallback onClearSelection;

  @override
  Widget build(BuildContext context) {
    final suffix = selectedCount == 0 ? '' : ' ($selectedCount)';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            OfflineAware(
              builder: (context, canWrite) => FilledButton.tonalIcon(
                key: const Key('dm-short-rest'),
                onPressed: canWrite ? onShortRest : null,
                icon: const AppIcon(AppIcons.campfire, size: 20),
                label: Text('Descanso corto$suffix'),
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => FilledButton.tonalIcon(
                key: const Key('dm-long-rest'),
                onPressed: canWrite ? onLongRest : null,
                icon: const AppIcon(AppIcons.moon, size: 20),
                label: Text('Descanso largo$suffix'),
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => FilledButton.tonalIcon(
                key: const Key('dm-message'),
                onPressed: canWrite ? onMessage : null,
                icon: const AppIcon(AppIcons.envelope, size: 20),
                label: const Text('Mensaje secreto'),
              ),
            ),
            FilledButton.tonalIcon(
              key: const Key('dm-dice'),
              onPressed: () => showDiceSheet(context),
              icon: const AppIcon(AppIcons.d20, size: 20),
              label: const Text('Dados'),
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
                              icon: const Icon(Icons.storefront_outlined),
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

/// Count of the pending change requests with a link to the list.
class _ChangeRequestsCard extends ConsumerWidget {
  const _ChangeRequestsCard({required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pending = ref.watch(pendingChangeRequestCountProvider(campaignId));
    return ParchmentCard(
      margin: const EdgeInsets.symmetric(vertical: 6),
      child: ListTile(
        key: const Key('dm-change-requests'),
        leading: Badge(
          isLabelVisible: pending > 0,
          label: Text('$pending'),
          child: const Icon(Icons.fact_check_outlined),
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
    );
  }
}
