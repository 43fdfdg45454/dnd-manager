import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/catalog/catalog_models.dart';
import '../../core/characters/change_detail.dart';
import '../../core/realtime/realtime_events.dart';
import '../../core/systems/game_system_ui.dart';
import '../../core/theme/icons.dart';
import '../../features/catalog/data/catalog_repository.dart';
import '../../features/catalog/data/models.dart' show ItemDetail;
import '../../features/catalog/ui/compendium_page.dart';
import '../../features/characters/domain/change_details.dart' show describeDnd5eChange;
import '../../features/characters/domain/character_format.dart' show copperToGoldText;
import '../../features/characters/domain/class_theme.dart' show classThemes;
import '../../features/characters/ui/character_tabs.dart';
import '../../features/characters/ui/combat/combat_view.dart';
import '../../features/characters/ui/sheet_editor_page.dart' show SheetEditorForm;
import '../../features/dice/domain/dice_expression.dart';
import '../../features/items/data/items_controllers.dart';
import '../../features/items/data/models.dart';
import '../../features/items/domain/combat_usable.dart' as combat;
import '../../features/items/domain/item_form_data.dart';
import '../../features/items/ui/attunement_dialog.dart';
import '../../features/items/ui/item_fields_form.dart';
import '../../features/session/data/party_repository.dart';
import '../../features/characters/data/character_refresh.dart';
import 'characters/height_weight_roller.dart';
import 'characters/models.dart';
import 'dnd5e_events.dart';
import 'dnd5e_routes.dart';
import 'items/item_extras.dart';
import 'session/level_up_card.dart';
import 'session/party_panel.dart';

/// Attribution of the SRD 5.1, shown when the server does not send its own.
const dnd5eSrdAttribution =
    'Contenido del System Reference Document 5.1, bajo licencia Creative Commons '
    'Attribution 4.0 International (CC-BY 4.0).';

/// D&D 5e (SRD 5.1): the game system this app brings. For now every member
/// returns the widgets the app already had; phase 33B moves the pages that
/// still mix core and 5e to these members.
class Dnd5eUi extends GameSystemUi {
  const Dnd5eUi();

  @override
  String get id => 'dnd5e';

  @override
  String get name => 'D&D 5e (SRD 5.1)';

  @override
  List<SystemAttribution> get attributions => const [
    SystemAttribution(
      ruleset: 'SRD 5.1',
      text: dnd5eSrdAttribution,
      licenseTitle: 'Licencia CC-BY 4.0',
      licenseAsset: 'assets/licenses/CC-BY-4.0.txt',
    ),
  ];

  @override
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey) =>
      dnd5eRoutes(rootNavigatorKey);

  // -- Sheet ------------------------------------------------------------------

  @override
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    Widget? header,
  }) => CombatView(character: character, canEdit: canEdit, isDm: isDm, header: header);

  @override
  List<SheetTab> detailTabs(CharacterDetail character, {required bool canEdit, bool isDm = false}) =>
      [
        SheetTab(
          id: 'summary',
          label: 'Resumen',
          builder: (_) => SummaryTab(character: character, canEdit: canEdit, isDm: isDm),
        ),
        SheetTab(
          id: 'skills',
          label: 'Habilidades',
          builder: (_) => SkillsTab(character: character),
        ),
        SheetTab(id: 'traits', label: 'Rasgos', builder: (_) => TraitsTab(character: character)),
        SheetTab(id: 'spells', label: 'Hechizos', builder: (_) => SpellsTab(character: character)),
      ];

  @override
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character) =>
      character.pendingLevelUpTo == null ? null : LevelUpCard(character: character);

  @override
  Widget? sheetEditorSection(SheetEditorScope scope) => SheetEditorForm(character: scope.character);

  @override
  Widget? heightWeightRoller(HeightWeightScope scope) => Dnd5eHeightWeightRoller(scope: scope);

  // -- Creation ---------------------------------------------------------------

  @override
  Future<void> openCreationWizard(
    BuildContext context,
    String campaignId, {
    String? ownerUserId,
  }) => context.push(Dnd5eRoutes.characterWizard(campaignId, ownerUserId: ownerUserId));

  // -- Catalog and items --------------------------------------------------------

  @override
  List<CompendiumTab> compendiumTabs() => [
    CompendiumTab(id: 'spells', label: 'Hechizos', builder: (_) => const CompendiumSpellsTab()),
    CompendiumTab(id: 'items', label: 'Objetos', builder: (_) => const CompendiumItemsTab()),
    CompendiumTab(id: 'classes', label: 'Clases', builder: (_) => const CompendiumClassesTab()),
    CompendiumTab(id: 'races', label: 'Razas', builder: (_) => const CompendiumRacesTab()),
    CompendiumTab(id: 'beasts', label: 'Bestias', builder: (_) => const CompendiumBeastsTab()),
    CompendiumTab(
      id: 'conditions',
      label: 'Condiciones',
      builder: (_) => const CompendiumConditionsTab(),
    ),
    CompendiumTab(id: 'tables', label: 'Tablas', builder: (_) => const CompendiumTablesTab()),
  ];

  @override
  Future<List<CatalogSource>> catalogSources(Ref ref) =>
      ref.watch(catalogRepositoryProvider).sources();

  @override
  Widget itemExtras(EffectiveItem item) => Dnd5eItemExtras(item: item);

  /// The 5e fields of the item form ([ItemFieldsForm]); [ItemFormScope.formKey]
  /// becomes its key (a `GlobalKey<ItemFieldsFormState>` reads it back) and
  /// [ItemFormScope.initial] is read as a catalog template.
  @override
  Widget? itemFormSection(ItemFormScope scope) => ItemFieldsForm(
    key: scope.formKey,
    initial: scope.initial.isEmpty
        ? const ItemFormData()
        : ItemFormData.fromDetail(ItemDetail.fromJson(scope.initial)),
    templateMode: scope.templateMode,
  );

  @override
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false}) =>
      combat.isCombatUsable(item, hasCharges: hasCharges);

  @override
  Future<bool> openAttunement(BuildContext context, String characterId, CharacterItem item) {
    final container = ProviderScope.containerOf(context, listen: false);
    return attuneWithReplacement(
      context,
      item: item,
      attunedItems: [...?container.read(inventoryControllerProvider(characterId)).value?.items],
      write: (patch) =>
          container.read(inventoryControllerProvider(characterId).notifier).patch(item.id, patch),
    );
  }

  // -- Campaign -----------------------------------------------------------------

  @override
  Widget partyPanel(BuildContext context, String campaignId) =>
      Dnd5ePartyPanel(campaignId: campaignId);

  /// "Raza · Clases · Nivel N" (or "Sin clase").
  @override
  String rosterSubtitle(CharacterSummary summary) {
    final line = Dnd5eRosterLine.of(summary);
    return [
      ?line.raceName,
      line.classes.isEmpty ? 'Sin clase' : classesLabel(line.classes),
      if (line.classes.isNotEmpty) 'Nivel ${line.level}',
    ].join(' · ');
  }

  @override
  ChangeDetail? describeChangeRequest(ChangeRequest request) => describeDnd5eChange(request);

  @override
  String? staleScope(String campaignId) => PartyRepository.partyPath(campaignId);

  // -- Dice, money, realtime ----------------------------------------------------

  /// A natural 20 is a critical and a natural 1 a fumble.
  @override
  RollClass classifyRoll(DiceResult result) => result.isCritical
      ? RollClass.critical
      : result.isFumble
      ? RollClass.fumble
      : RollClass.normal;

  @override
  String formatMoney(int minorUnits) => copperToGoldText(minorUnits);

  /// The icon of the class named in a class or feature part.
  @override
  AppIcons? breakdownIcon(BreakdownPart part) {
    if (part.source != 'class' && part.source != 'feature') return null;
    final label = part.label.toLowerCase();
    for (final theme in classThemes.values) {
      if (label.contains(theme.labelEs.toLowerCase())) return theme.icon;
    }
    return null;
  }

  /// A party rest refreshes every character of the campaign; a granted level,
  /// the character (the banner of the player is shown by the campaign shell).
  @override
  void onRealtimeEvent(Ref ref, UnknownCampaignEvent event) {
    switch (dnd5eEventOf(event)) {
      case PartyRest(:final campaignId):
        refreshCampaignCharacters(ref, campaignId, null);
      case LevelUpGranted(:final campaignId, :final characterId):
        refreshCampaignCharacters(ref, campaignId, characterId);
      default:
        break;
    }
  }
}
