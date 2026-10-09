import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../characters/ui/characters_tab.dart';
import '../data/campaigns_controller.dart';

/// "Personajes" view of a campaign: the character list of the campaign as its
/// own branch of the campaign bar (the shell draws the app bar).
class CampaignCharactersPage extends ConsumerWidget {
  const CampaignCharactersPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final campaign = ref.watch(campaignDetailControllerProvider(campaignId)).value;
    if (campaign == null) return const Center(child: CircularProgressIndicator());
    return KeyedSubtree(key: const Key('campaign-characters'), child: CharactersTab(campaign: campaign));
  }
}
