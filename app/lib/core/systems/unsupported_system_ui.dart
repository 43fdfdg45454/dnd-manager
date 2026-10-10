import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable, ProviderOrFamily;
import 'package:go_router/go_router.dart';

import '../../features/dice/domain/dice_expression.dart';
import '../../features/items/data/models.dart';
import '../../features/items/domain/items_format.dart' show formatWeightLb;
import '../catalog/catalog_models.dart';
import '../characters/change_detail.dart';
import '../characters/models.dart';
import '../realtime/realtime_events.dart';
import '../theme/icons.dart';
import '../ui/breakdown.dart';
import 'game_system_ui.dart';

/// Nobody at the table of a campaign whose system the app does not bring.
final _noTableCharacters = Provider<List<TableCharacter>>((ref) => const []);

/// Catalog templates of a system the app does not bring: none can be loaded.
final _noItemTemplate = FutureProvider.family<ItemSummary, String>(
  (ref, templateId) => throw StateError('Plantilla $templateId no disponible'),
);

/// "Esta app no incluye el sistema X": what the app shows for a campaign of a
/// game system it does not bring. The sheet, the compendium and the DM table
/// show the notice; the rest of the campaign (lore, maps, sessions, shops...)
/// works as usual.
class UnsupportedSystemUi extends GameSystemUi {
  const UnsupportedSystemUi(this.id);

  @override
  final String id;

  @override
  String get name => id;

  /// "Esta app no incluye el sistema [id]."
  String get notice => 'Esta app no incluye el sistema $id.';

  Widget _notice() => UnsupportedSystemNotice(systemId: id);

  @override
  List<SystemAttribution> get attributions => const [];

  @override
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey) => const [];

  // -- Sheet ------------------------------------------------------------------

  @override
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    bool inSession = false,
    Widget? header,
  }) => header == null
      ? _notice()
      : ListView(
          key: const Key('player-combat'),
          padding: const EdgeInsets.fromLTRB(12, 4, 12, 32),
          children: [header, _notice()],
        );

  @override
  List<SheetTab> detailTabs(
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
  }) => [SheetTab(id: 'sheet', label: 'Hoja', builder: (_) => _notice())];

  @override
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character) => null;

  @override
  String? pendingActionRoute(CharacterDetail character) => null;

  @override
  List<Widget> sessionCards(BuildContext context, CharacterDetail character) => const [];

  @override
  bool isDown(CharacterDetail character) => false;

  @override
  Widget characterAccent(CharacterDetail character, {required Widget child}) => child;

  @override
  Widget? characterHeadline(CharacterDetail character, {bool compact = false}) => null;

  @override
  List<Widget> characterBadges(CharacterDetail character) => const [];

  @override
  Widget? notesFacts(CharacterDetail character) => null;

  @override
  Widget? sheetEditorSection(SheetEditorScope scope) => null;

  @override
  Widget? heightWeightRoller(HeightWeightScope scope) => null;

  // -- Creation ---------------------------------------------------------------

  @override
  Future<void> openCreationWizard(
    BuildContext context,
    String campaignId, {
    String? ownerUserId,
  }) async {
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(notice)));
  }

  // -- Catalog and items --------------------------------------------------------

  @override
  List<CompendiumTab> compendiumTabs() => [
    CompendiumTab(id: 'unsupported', label: name, builder: (_) => _notice()),
  ];

  @override
  Future<List<CatalogSource>> catalogSources(Ref ref) async => const [];

  @override
  Future<void> openCatalogItem(BuildContext context, String itemId) async {
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(notice)));
  }

  @override
  List<String> itemSummaryFacts(ItemSummary item) => const [];

  @override
  Widget itemExtras(EffectiveItem item) => const SizedBox.shrink();

  @override
  List<ItemFact> itemFacts(EffectiveItem item) => [
    (label: 'Peso', value: formatWeightLb(item.weightLb), fields: const ['weightLb']),
  ];

  @override
  ItemModifiersView? itemModifiers(EffectiveItem item) => null;

  @override
  FutureProvider<ItemSummary> itemTemplate(String templateId) => _noItemTemplate(templateId);

  @override
  void onItemTemplateChanged(Ref ref, String templateId) {}

  @override
  Widget? itemFormSection(ItemFormScope scope) => null;

  @override
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false}) =>
      hasCharges || item.isConsumable;

  @override
  bool canEquip(EffectiveItem item) => !item.isConsumable;

  @override
  int get maxAttunedItems => 0;

  @override
  bool requiresAttunement(EffectiveItem item) => false;

  @override
  Future<bool> openAttunement(BuildContext context, String characterId, CharacterItem item) async =>
      false;

  // -- Campaign -----------------------------------------------------------------

  @override
  Widget partyPanel(BuildContext context, String campaignId) => _notice();

  @override
  ProviderListenable<List<TableCharacter>> tableCharacters(String campaignId) => _noTableCharacters;

  @override
  List<ProviderOrFamily> tableProviders(String campaignId) => const [];

  @override
  String rosterSubtitle(CharacterSummary summary) => '';

  @override
  String? rosterStatus(CharacterSummary summary) => null;

  @override
  ChangeDetail? describeChangeRequest(ChangeRequest request) => null;

  @override
  String? staleScope(String campaignId) => null;

  @override
  SystemAccent? get sampleAccent => null;

  // -- Dice, money, realtime ----------------------------------------------------

  @override
  RollClass classifyRoll(DiceResult result) => RollClass.normal;

  @override
  String formatMoney(int minorUnits) => '$minorUnits';

  @override
  String formatPrice(int minorUnits) => '$minorUnits';

  @override
  String moneyInputText(int minorUnits) => '$minorUnits';

  @override
  int? parseMoney(String text, {bool allowNegative = false}) {
    final value = int.tryParse(text.trim());
    if (value == null || (!allowNegative && value < 0)) return null;
    return value;
  }

  @override
  AppIcons? breakdownIcon(BreakdownPart part) => null;

  @override
  void onRealtimeEvent(Ref ref, UnknownCampaignEvent event) {}

  @override
  Future<CampaignEventNotice?> playerNotice(
    UnknownCampaignEvent event, {
    required Future<CharacterDetail?> Function(String characterId) ownCharacter,
  }) async => null;
}

/// The notice of [UnsupportedSystemUi]: "Esta app no incluye el sistema X".
class UnsupportedSystemNotice extends StatelessWidget {
  const UnsupportedSystemNotice({super.key, required this.systemId});

  final String systemId;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Text(
        'Esta app no incluye el sistema $systemId.',
        key: const Key('unsupported-system'),
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyLarge,
      ),
    ),
  );
}
