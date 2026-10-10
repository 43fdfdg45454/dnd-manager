import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../systems/system_registry.dart';
import 'catalog_models.dart';

/// A counter the core bumps when the catalog of the instance changes (a
/// content pack is imported or deleted). The catalog providers of the game
/// systems watch it, so they reload without the core knowing them.
class CatalogRevision extends Notifier<int> {
  @override
  int build() => 0;

  /// The catalog changed: every provider that watches the revision reloads.
  void bump() => state++;
}

final catalogRevisionProvider = NotifierProvider<CatalogRevision, int>(CatalogRevision.new);

/// A counter per campaign the core bumps when the content packs the campaign
/// enables change (its own save, or the `campaign.updated` realtime event).
/// The catalog providers scoped to a campaign watch it and reload.
class CampaignCatalogRevision extends Notifier<int> {
  CampaignCatalogRevision(this.campaignId);

  final String campaignId;

  @override
  int build() => 0;

  void bump() => state++;
}

final campaignCatalogRevisionProvider =
    NotifierProvider.family<CampaignCatalogRevision, int, String>(CampaignCatalogRevision.new);

/// Sources of the catalog (SRD and content packs) of the default game system,
/// to name the pack a piece of content comes from. Kept alive: it is tiny and
/// every chip reads it. Errors (offline without cache) leave the chips showing
/// nothing rather than failing.
final catalogSourcesProvider = FutureProvider<List<CatalogSource>>((ref) {
  ref.watch(catalogRevisionProvider);
  return ref.watch(defaultGameSystemUiProvider).catalogSources(ref);
}, retry: (retryCount, error) => null);

/// The sources of the catalog for the compendium opened from [campaignId]
/// (each one with `enabled`) or, with null, from the main menu. Reloads when
/// the catalog or the packs of the campaign change.
final compendiumSourcesProvider = FutureProvider.autoDispose.family<List<CatalogSource>, String?>((
  ref,
  campaignId,
) {
  ref.watch(catalogRevisionProvider);
  if (campaignId == null) {
    return ref.watch(defaultGameSystemUiProvider).catalogSources(ref);
  }
  ref.watch(campaignCatalogRevisionProvider(campaignId));
  return ref
      .watch(campaignSystemUiProvider(campaignId))
      .catalogSources(ref, campaignId: campaignId);
}, retry: (retryCount, error) => null);
