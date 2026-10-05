import 'package:flutter/material.dart' hide Page;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../data/items_controllers.dart';
import '../data/models.dart';
import '../../catalog/domain/catalog_format.dart';
import 'item_feedback.dart';

/// Purchase and sale history of a campaign: DMs see every transaction, players
/// those of their own characters.
class TransactionsPage extends ConsumerWidget {
  const TransactionsPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transactions = ref.watch(transactionsControllerProvider(campaignId));
    return Scaffold(
      appBar: AppBar(title: const Text('Transacciones')),
      body: transactions.when(
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
                    Center(child: Text('Aún no hay transacciones', key: Key('transactions-empty'))),
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
    );
  }
}

class _TransactionTile extends StatelessWidget {
  const _TransactionTile({required this.transaction});

  final Transaction transaction;

  @override
  Widget build(BuildContext context) {
    final t = transaction;
    final theme = Theme.of(context);
    final isPurchase = t.type == TransactionType.purchase;
    final date = t.at == null ? null : DateFormat('dd/MM/yyyy HH:mm').format(t.at!.toLocal());
    return ListTile(
      key: Key('transaction-${t.id}'),
      leading: Icon(isPurchase ? Icons.shopping_cart_outlined : Icons.sell_outlined),
      title: Text('${t.itemName} ×${t.quantity}'),
      subtitle: Text([t.characterName, t.shopName, ?date].where((e) => e.isNotEmpty).join(' · ')),
      trailing: Text(
        '${isPurchase ? '-' : '+'}${formatCostCp(t.totalCp)}',
        style: theme.textTheme.titleSmall?.copyWith(
          color: isPurchase ? theme.colorScheme.error : null,
        ),
      ),
    );
  }
}
