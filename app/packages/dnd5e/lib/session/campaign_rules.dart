import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/features/content_packs/data/campaign_content_packs_controller.dart';

import '../characters/dnd5e_characters_repository.dart';

/// D&D 5e rules of a campaign (`/systems/dnd5e/campaigns/{id}/rules`).
class CampaignRules {
  const CampaignRules({this.starterFeatsEnabled = false, this.starterFeatsAvailable = false});

  factory CampaignRules.fromJson(Map<String, dynamic> json) => CampaignRules(
    starterFeatsEnabled: json['starterFeatsEnabled'] as bool? ?? false,
    starterFeatsAvailable: json['starterFeatsAvailable'] as bool? ?? false,
  );

  /// The Owner or a DM allows a starter feat at creation.
  final bool starterFeatsEnabled;

  /// Some enabled content pack of the campaign brings starter feats.
  final bool starterFeatsAvailable;

  /// The creation wizard offers the optional "Dote inicial" step.
  bool get offersStarterFeat => starterFeatsEnabled && starterFeatsAvailable;
}

/// The rules of a campaign; reloaded when its content packs change (they
/// decide whether starter feats exist).
class CampaignRulesController extends AsyncNotifier<CampaignRules> {
  CampaignRulesController(this.campaignId);

  final String campaignId;

  @override
  Future<CampaignRules> build() {
    ref.watch(campaignContentPacksControllerProvider(campaignId).select((v) => v.value));
    return ref.read(dnd5eCharactersRepositoryProvider).campaignRules(campaignId);
  }

  /// Turns the starter feats on or off. Errors are rethrown.
  Future<void> setStarterFeats(bool enabled) async {
    state = AsyncData(
      await ref
          .read(dnd5eCharactersRepositoryProvider)
          .saveCampaignRules(campaignId, starterFeatsEnabled: enabled),
    );
  }
}

final campaignRulesControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignRulesController, CampaignRules, String>(
      CampaignRulesController.new,
      retry: (retryCount, error) => null,
    );
