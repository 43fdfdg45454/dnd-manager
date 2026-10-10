import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/characters/models.dart' show RestKind, RestRequest;
import '../../../../core/motion/rest_celebration.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/systems/system_registry.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../characters/data/characters_controller.dart';
import '../../../items/data/items_controllers.dart';
import '../../../items/data/models.dart' show ShopSummary;
import '../../../sessions/ui/next_session_card.dart';
import '../../data/session_controllers.dart';
import '../session_feedback.dart';
import '../stash_card.dart';

/// "Mesa del DM": the party panel of the game system (D&D 5e: the party at a
/// glance with forced rests, level grants, secret messages and dice), the
/// party stash, the shops with their open/closed switch,
/// the petitions (rest requests and change requests) and the next session. For
/// DMs and the Owner.
class DmSessionPage extends ConsumerStatefulWidget {
  const DmSessionPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<DmSessionPage> createState() => _DmSessionPageState();
}

class _DmSessionPageState extends ConsumerState<DmSessionPage> {
  String get _campaignId => widget.campaignId;

  @override
  Widget build(BuildContext context) {
    final campaign = ref.watch(campaignDetailControllerProvider(_campaignId)).value;
    if (campaign == null) return const Center(child: CircularProgressIndicator());
    final system = ref.watch(campaignSystemUiProvider(_campaignId));
    final characters = ref.watch(system.tableCharacters(_campaignId));

    return RefreshIndicator(
      onRefresh: () async {
        for (final provider in system.tableProviders(_campaignId)) {
          ref.invalidate(provider);
        }
        ref.invalidate(restRequestsControllerProvider(_campaignId));
        ref.invalidate(stashControllerProvider(_campaignId));
        ref.invalidate(shopsControllerProvider(_campaignId));
      },
      child: ListView(
        key: const Key('dm-session'),
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 32),
        children: [
          system.partyPanel(context, _campaignId),
          const SizedBox(height: 8),
          PartyStashCard(campaign: campaign, characters: characters),
          _ShopsCard(campaign: campaign),
          _PetitionsCard(campaignId: _campaignId),
          const NextSessionCard(),
        ],
      ),
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
