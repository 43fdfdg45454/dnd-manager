import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Search text shared by every tab of a compendium (already debounced by the
/// UI). The argument is the campaign the compendium was opened from (null:
/// the compendium of the main menu), so both can be open at once.
class CompendiumSearch extends Notifier<String> {
  CompendiumSearch(this.campaignId);

  final String? campaignId;

  @override
  String build() => '';

  void set(String value) => state = value.trim();
}

final compendiumSearchProvider = NotifierProvider.autoDispose
    .family<CompendiumSearch, String, String?>(CompendiumSearch.new);

/// What a compendium shows besides the search: [campaignOnly] narrows the
/// catalog to the content packs the campaign enables (only when it was opened
/// from a campaign) and [source] keeps the content of one source (the id of
/// the base pack or of a content pack; null: every source).
@immutable
class CompendiumFilter {
  const CompendiumFilter({this.campaignOnly = false, this.source});

  final bool campaignOnly;
  final String? source;

  /// Whether [itemSource] passes the source filter (an item without source
  /// counts as the base pack, "srd").
  bool matchesSource(String? itemSource) {
    final wanted = source;
    if (wanted == null) return true;
    return (itemSource == null || itemSource.isEmpty ? 'srd' : itemSource) == wanted;
  }

  @override
  bool operator ==(Object other) =>
      other is CompendiumFilter && other.campaignOnly == campaignOnly && other.source == source;

  @override
  int get hashCode => Object.hash(campaignOnly, source);
}

/// The [CompendiumFilter] of the compendium opened from a campaign (the
/// argument; null for the one of the main menu). From a campaign it starts
/// with "Solo lo activo en la campaña" on.
class CompendiumFilterController extends Notifier<CompendiumFilter> {
  CompendiumFilterController(this.campaignId);

  final String? campaignId;

  @override
  CompendiumFilter build() => CompendiumFilter(campaignOnly: campaignId != null);

  void setCampaignOnly(bool value) =>
      state = CompendiumFilter(campaignOnly: campaignId != null && value, source: state.source);

  void setSource(String? source) =>
      state = CompendiumFilter(campaignOnly: state.campaignOnly, source: source);
}

final compendiumFilterProvider = NotifierProvider.autoDispose
    .family<CompendiumFilterController, CompendiumFilter, String?>(CompendiumFilterController.new);

/// The campaign whose catalog the lists of the compendium opened from
/// [pageCampaignId] ask for: the campaign while "Solo lo activo en la campaña"
/// is on, null (the global catalog) otherwise.
final compendiumCatalogCampaignProvider = Provider.autoDispose.family<String?, String?>(
  (ref, pageCampaignId) =>
      ref.watch(compendiumFilterProvider(pageCampaignId).select((f) => f.campaignOnly))
      ? pageCampaignId
      : null,
);

/// Tells the tabs of a compendium which page they belong to: the campaign it
/// was opened from, or null for the compendium of the main menu.
class CompendiumScope extends InheritedWidget {
  const CompendiumScope({super.key, required this.campaignId, required super.child});

  final String? campaignId;

  /// The campaign of the closest compendium (null outside one or in the
  /// compendium of the main menu).
  static String? of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<CompendiumScope>()?.campaignId;

  @override
  bool updateShouldNotify(CompendiumScope oldWidget) => oldWidget.campaignId != campaignId;
}
