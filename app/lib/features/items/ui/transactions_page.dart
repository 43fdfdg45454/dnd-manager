import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/cache/stale_data.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_repository.dart';
import '../data/items_controllers.dart';
import '../data/models.dart';
import '../../catalog/domain/catalog_format.dart';
import 'item_feedback.dart';

/// Transaction history of a campaign (purchases, sales and the movements of the
/// party stash): DMs see every transaction, players those of their own
/// characters.
class TransactionsPage extends ConsumerWidget {
  const TransactionsPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transactions = ref.watch(transactionsControllerProvider(campaignId));
    return Scaffold(
      appBar: AppBar(title: const Text('Transacciones')),
      body: OfflineBannerLayout(
        scopes: [staleTree('${CampaignsRepository.campaignPath(campaignId)}/transactions')],
        child: transactions.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ItemsErrorView(
            error: error,
            onRetry: () => ref.invalidate(transactionsControllerProvider(campaignId)),
          ),
          data: (page) => RefreshIndicator(
            onRefresh: () async => ref.invalidate(transactionsControllerProvider(campaignId)),
            child: page.items.isEmpty
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    children: const [
                      SizedBox(height: 96),
                      Center(
                        child: Text('Aún no hay transacciones', key: Key('transactions-empty')),
                      ),
                    ],
                  )
                : ListView.builder(
                    itemCount: page.items.length + (page.hasMore ? 1 : 0),
                    itemBuilder: (context, index) {
                      if (index >= page.items.length) {
                        return Padding(
                          padding: const EdgeInsets.all(8),
                          child: Center(
                            child: TextButton(
                              key: const Key('transactions-more'),
                              onPressed: () => ref
                                  .read(transactionsControllerProvider(campaignId).notifier)
                                  .loadMore(),
                              child: const Text('Cargar más'),
                            ),
                          ),
                        );
                      }
                      return _TransactionTile(transaction: page.items[index]);
                    },
                  ),
          ),
        ),
      ),
    );
  }
}

class _TransactionTile extends StatelessWidget {
  const _TransactionTile({required this.transaction});

  final Transaction transaction;

  static IconData _icon(TransactionType type) => switch (type) {
    TransactionType.purchase => Icons.shopping_cart_outlined,
    TransactionType.sale => Icons.sell_outlined,
    TransactionType.stashAdd => Icons.add_box_outlined,
    TransactionType.stashRemove => Icons.indeterminate_check_box_outlined,
    TransactionType.stashTake => Icons.move_to_inbox_outlined,
    TransactionType.stashReturn => Icons.outbox_outlined,
    TransactionType.stashGoldAdd => Icons.savings_outlined,
    TransactionType.stashGoldSplit => Icons.call_split,
  };

  @override
  Widget build(BuildContext context) {
    final t = transaction;
    final theme = Theme.of(context);
    final date = t.at == null ? null : DateFormat('dd/MM/yyyy HH:mm').format(t.at!.toLocal());
    final isTrade = t.type == TransactionType.purchase || t.type == TransactionType.sale;
    final isGold =
        t.type == TransactionType.stashGoldAdd || t.type == TransactionType.stashGoldSplit;

    // Money: what a character pays (-) or gets (+) in a trade or a gold share;
    // the stash gold as it is; nothing for the loot moves.
    final String? amount = switch (t.type) {
      TransactionType.purchase => '-${formatCostCp(t.totalCp)}',
      TransactionType.sale || TransactionType.stashGoldSplit => '+${formatCostCp(t.totalCp)}',
      TransactionType.stashGoldAdd => formatCostCp(t.totalCp),
      _ => null,
    };
    final subtitle = [
      if (!isTrade) t.type.label,
      t.characterName,
      t.shopName,
      if (!isTrade && t.actorDisplayName.isNotEmpty) 'por ${t.actorDisplayName}',
      ?date,
    ].where((e) => e.isNotEmpty).join(' · ');

    return ListTile(
      key: Key('transaction-${t.id}'),
      leading: Icon(_icon(t.type)),
      title: Text(isGold ? t.itemName : '${t.itemName} ×${t.quantity}'),
      subtitle: Text(subtitle),
      trailing: amount == null
          ? null
          : Text(
              amount,
              style: theme.textTheme.titleSmall?.copyWith(
                color: t.type == TransactionType.purchase ? theme.colorScheme.error : null,
              ),
            ),
    );
  }
}
