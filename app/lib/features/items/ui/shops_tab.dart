import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';
import 'item_feedback.dart';
import 'shop_dialogs.dart';

/// "Tiendas" tab of a campaign. Players see the open shops; DMs see all of them
/// with an open/closed switch and can create new ones.
class ShopsTab extends ConsumerWidget {
  const ShopsTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  Future<void> _create(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<ShopFormData>(
      context: context,
      builder: (_) => const ShopFormDialog(title: 'Nueva tienda'),
    );
    if (data == null || !context.mounted) return;
    await runItemAction(
      context,
      () => ref
          .read(shopsControllerProvider(campaign.id).notifier)
          .create(
            name: data.name,
            description: data.description,
            buybackPercent: data.buybackPercent,
          ),
      success: 'Tienda creada.',
    );
  }

  Future<void> _toggle(BuildContext context, WidgetRef ref, ShopSummary shop, bool open) =>
      runItemAction(
        context,
        () => ref.read(shopsControllerProvider(campaign.id).notifier).setOpen(shop.id, open),
        success: open ? '${shop.name} está abierta.' : '${shop.name} está cerrada.',
      );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = campaign.myRole.isAtLeastDm;
    final shops = ref.watch(shopsControllerProvider(campaign.id));

    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: isDm
          ? FloatingActionButton.extended(
              key: const Key('shops-new'),
              onPressed: () => _create(context, ref),
              icon: const Icon(Icons.add_business_outlined),
              label: const Text('Nueva tienda'),
            )
          : null,
      body: shops.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ItemsErrorView(
          error: error,
          onRetry: () => ref.invalidate(shopsControllerProvider(campaign.id)),
        ),
        data: (list) => RefreshIndicator(
          onRefresh: () => ref.read(shopsControllerProvider(campaign.id).notifier).reload(),
          child: list.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    const SizedBox(height: 96),
                    Center(
                      child: Text(
                        isDm ? 'Aún no hay tiendas en esta campaña' : 'No hay tiendas abiertas',
                        key: const Key('shops-empty'),
                      ),
                    ),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.only(top: 8, bottom: 96),
                  children: [
                    for (final shop in list)
                      _ShopCard(
                        shop: shop,
                        isDm: isDm,
                        onOpen: () => context.push(AppRoutes.shop(campaign.id, shop.id)),
                        onToggle: (open) => _toggle(context, ref, shop, open),
                      ),
                  ],
                ),
        ),
      ),
    );
  }
}

class _ShopCard extends StatelessWidget {
  const _ShopCard({
    required this.shop,
    required this.isDm,
    required this.onOpen,
    required this.onToggle,
  });

  final ShopSummary shop;
  final bool isDm;
  final VoidCallback onOpen;
  final ValueChanged<bool> onToggle;

  @override
  Widget build(BuildContext context) {
    final description = shop.description;
    return Card(
      key: Key('shop-${shop.id}'),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(shop.name, style: Theme.of(context).textTheme.titleMedium),
                    if (description != null && description.isNotEmpty)
                      Text(description, maxLines: 2, overflow: TextOverflow.ellipsis),
                    Text(
                      'Recompra ${shop.buybackPercent} %',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              if (isDm)
                Column(
                  children: [
                    Switch(
                      key: Key('shop-switch-${shop.id}'),
                      value: shop.isOpen,
                      onChanged: onToggle,
                    ),
                    Text(
                      shop.isOpen ? 'Abierta' : 'Cerrada',
                      style: Theme.of(context).textTheme.labelSmall,
                    ),
                  ],
                )
              else
                const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}
