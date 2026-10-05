import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../data/campaigns_controller.dart';
import '../domain/campaign_models.dart';
import 'campaign_form_dialog.dart';
import 'feedback.dart';

/// List of the user's campaigns with a "new campaign" action. Used as the body
/// of the home page; it has its own [Scaffold] to host the floating button.
class CampaignsPage extends ConsumerWidget {
  const CampaignsPage({super.key});

  Future<void> _createCampaign(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<CampaignFormData>(
      context: context,
      builder: (_) => const CampaignFormDialog(title: 'Nueva campaña', submitLabel: 'Crear'),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => ref
          .read(campaignsControllerProvider.notifier)
          .create(name: data.name, description: data.description),
      success: 'Campaña creada.',
      errors: const {400: 'Datos no válidos. Revisa el nombre y la descripción.'},
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final campaigns = ref.watch(campaignsControllerProvider);

    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        key: const Key('campaigns-new'),
        onPressed: () => _createCampaign(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('Nueva campaña'),
      ),
      body: campaigns.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeCampaignError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(campaignsControllerProvider),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
        data: (items) {
          if (items.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text('Aún no tienes campañas', textAlign: TextAlign.center),
              ),
            );
          }
          return RefreshIndicator(
            onRefresh: ref.read(campaignsControllerProvider.notifier).reload,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 88),
              children: [for (final campaign in items) _CampaignCard(campaign: campaign)],
            ),
          );
        },
      ),
    );
  }
}

class _CampaignCard extends StatelessWidget {
  const _CampaignCard({required this.campaign});

  final CampaignSummary campaign;

  @override
  Widget build(BuildContext context) {
    final members = campaign.memberCount == 1 ? '1 miembro' : '${campaign.memberCount} miembros';
    return Card(
      key: Key('campaign-${campaign.id}'),
      child: ListTile(
        title: Text(campaign.name),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Wrap(
            spacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Chip(
                label: Text(campaign.myRole.label),
                visualDensity: VisualDensity.compact,
                padding: EdgeInsets.zero,
              ),
              Text(members),
            ],
          ),
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => context.push(AppRoutes.campaign(campaign.id)),
      ),
    );
  }
}
