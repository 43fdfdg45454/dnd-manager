import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/offline_widgets.dart';
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

  Future<void> _reload(WidgetRef ref) => Future.wait([
    ref.read(campaignsControllerProvider.notifier).reload(),
    ref.read(myInvitationsControllerProvider.notifier).reload(),
  ]);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final campaigns = ref.watch(campaignsControllerProvider);

    return Scaffold(
      floatingActionButton: OfflineAwareFab(
        fabKey: const Key('campaigns-new'),
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
          final invitations = ref.watch(myInvitationsControllerProvider).value ?? const <MyInvitation>[];
          if (items.isEmpty && invitations.isEmpty) {
            return RefreshIndicator(
              onRefresh: () => _reload(ref),
              child: ListView(
                children: const [
                  Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Aún no tienes campañas', textAlign: TextAlign.center),
                  ),
                ],
              ),
            );
          }
          return RefreshIndicator(
            onRefresh: () => _reload(ref),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 88),
              children: [
                for (final invitation in invitations) _InvitationCard(invitation: invitation),
                for (final campaign in items) _CampaignCard(campaign: campaign),
              ],
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

/// A pending invitation: accept it (and open the campaign) or decline it.
class _InvitationCard extends ConsumerWidget {
  const _InvitationCard({required this.invitation});

  final MyInvitation invitation;

  Future<void> _accept(BuildContext context, WidgetRef ref) async {
    final router = GoRouter.maybeOf(context);
    final ok = await runAction(
      context,
      () => ref.read(myInvitationsControllerProvider.notifier).accept(invitation),
      success: 'Te has unido a ${invitation.campaignName}.',
      errors: const {404: 'La invitación ya no existe.', 409: 'Ya eres miembro de esta campaña.'},
    );
    if (ok) router?.push(AppRoutes.campaign(invitation.campaignId));
  }

  Future<void> _decline(BuildContext context, WidgetRef ref) => runAction(
    context,
    () => ref.read(myInvitationsControllerProvider.notifier).decline(invitation),
    success: 'Invitación rechazada.',
    errors: const {404: 'La invitación ya no existe.'},
  );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return Card(
      key: Key('invitation-${invitation.id}'),
      color: theme.colorScheme.secondaryContainer,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Invitación a ${invitation.campaignName}', style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              '${invitation.invitedByDisplayName} te invita como ${invitation.role.label}.',
              style: theme.textTheme.bodyMedium,
            ),
            const SizedBox(height: 8),
            OfflineAware(
              builder: (context, canWrite) => Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    key: Key('invitation-decline-${invitation.id}'),
                    onPressed: !canWrite ? null : () => _decline(context, ref),
                    child: const Text('Rechazar'),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    key: Key('invitation-accept-${invitation.id}'),
                    onPressed: !canWrite ? null : () => _accept(context, ref),
                    child: const Text('Aceptar'),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
