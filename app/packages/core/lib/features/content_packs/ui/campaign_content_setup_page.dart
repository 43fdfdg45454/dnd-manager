import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import 'campaign_content_packs_section.dart';

/// "Contenido": the step right after creating a campaign. It shows the same
/// "Contenido y reglas" section as "Ajustes" so the owner picks the content
/// packs and rules first, and continues to the campaign.
class CampaignContentSetupPage extends StatelessWidget {
  const CampaignContentSetupPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Contenido')),
      body: ListView(
        key: const Key('campaign-content-setup'),
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Elige el contenido y las reglas de la campaña. '
            'Puedes cambiarlos cuando quieras en Ajustes.',
            style: theme.textTheme.bodyMedium,
          ),
          const SizedBox(height: 16),
          CampaignContentPacksSection(campaignId: campaignId, editable: true),
          const SizedBox(height: 16),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              key: const Key('campaign-content-setup-continue'),
              onPressed: () => context.pushReplacement(AppRoutes.campaign(campaignId)),
              icon: const Icon(Icons.arrow_forward),
              label: const Text('Continuar a la campaña'),
            ),
          ),
        ],
      ),
    );
  }
}
