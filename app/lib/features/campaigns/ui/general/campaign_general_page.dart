import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/tokens.dart';
import '../../../systems/data/systems_repository.dart';
import '../../../systems/domain/game_system.dart';
import '../../data/campaigns_controller.dart';
import '../../domain/campaign_models.dart';
import 'campaign_section_page.dart';

/// "Campaña" view of a campaign, for every member: the campaign at a glance
/// and a grid of cards that open each section as a full page (the characters
/// have their own tab in the bar).
class CampaignGeneralPage extends ConsumerWidget {
  const CampaignGeneralPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId)).value;
    if (campaign == null) return const Center(child: CircularProgressIndicator());
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = (constraints.maxWidth / 180).floor().clamp(2, 5);
        return ListView(
          key: const Key('campaign-general'),
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            _Intro(campaign: campaign),
            GridView.count(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: 12),
              crossAxisCount: columns,
              mainAxisSpacing: 4,
              crossAxisSpacing: 4,
              childAspectRatio: 1.25,
              children: [
                for (final section in CampaignSection.values)
                  if (section != CampaignSection.characters)
                    _SectionCard(
                      section: section,
                      onTap: () => context.push(AppRoutes.campaignSection(campaign.id, section)),
                    ),
              ],
            ),
          ],
        );
      },
    );
  }
}

class _Intro extends ConsumerWidget {
  const _Intro({required this.campaign});

  final CampaignDetail campaign;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final description = campaign.description.trim();
    final systemName = gameSystemName(ref.watch(systemsProvider).value, campaign.systemId);
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Dueño: ${campaign.ownerDisplayName} · Tu rol: ${campaign.myRole.label}',
            key: const Key('general-meta'),
            style: theme.textTheme.bodyMedium?.copyWith(color: context.tokens.inkMuted),
          ),
          Text(
            'Sistema de juego: $systemName',
            key: const Key('campaign-system'),
            style: theme.textTheme.bodyMedium?.copyWith(color: context.tokens.inkMuted),
          ),
          if (description.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(description, maxLines: 3, overflow: TextOverflow.ellipsis),
          ],
          const SectionHeader('Secciones', padding: EdgeInsets.only(top: 12, bottom: 4)),
        ],
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.section, required this.onTap});

  final CampaignSection section;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return ParchmentCard(
      key: Key('general-${section.path}'),
      margin: const EdgeInsets.all(4),
      onTap: onTap,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          AppIcon(section.icon, size: 40, color: tokens.gold),
          const SizedBox(height: 8),
          Text(
            section.label,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleSmall,
          ),
        ],
      ),
    );
  }
}
