import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable, ProviderOrFamily;
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/catalog/catalog_models.dart';
import 'package:opentrpg_core/core/characters/change_detail.dart';
import 'package:opentrpg_core/core/realtime/realtime_events.dart';
import 'package:opentrpg_core/core/systems/game_system_ui.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/features/characters/data/character_refresh.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/items/data/items_controllers.dart';
import 'package:opentrpg_core/features/items/data/models.dart';

import 'catalog/data/catalog_controllers.dart' show attributionTextProvider, itemDetailProvider;
import 'catalog/data/catalog_repository.dart';
import 'catalog/data/models.dart' show Dnd5eItemSummary;
import 'catalog/domain/catalog_format.dart' show formatCostCp, rarityLabel;
import 'catalog/ui/compendium_tabs.dart';
import 'characters/character_header.dart';
import 'characters/domain/change_details.dart' show describeDnd5eChange;
import 'characters/domain/character_format.dart' show copperToGoldText;
import 'characters/domain/class_theme.dart';
import 'characters/height_weight_roller.dart';
import 'characters/models.dart';
import 'characters/ui/character_tabs.dart';
import 'characters/ui/combat/combat_view.dart';
import 'characters/ui/combat/rest_section.dart';
import 'characters/ui/sheet_editor_form.dart';
import 'dnd5e_events.dart';
import 'dnd5e_routes.dart';
import 'items/dnd5e_item.dart' as dnd5e_item;
import 'items/domain/combat_usable.dart' as combat;
import 'items/item_extras.dart';
import 'items/item_facts.dart';
import 'items/money_format.dart' as money;
import 'items/ui/attunement_dialog.dart';
import 'items/ui/item_fields_form.dart';
import 'session/data/party_repository.dart';
import 'session/level_up_card.dart';
import 'session/party_controller.dart';
import 'session/party_models.dart';
import 'session/party_panel.dart';
import 'session/player_combat.dart';

/// Attribution of the SRD 5.1, shown when the server does not send its own.
const dnd5eSrdAttribution =
    'Contenido del System Reference Document 5.1, bajo licencia Creative Commons '
    'Attribution 4.0 International (CC-BY 4.0).';

/// D&D 5e (SRD 5.1): the game system this app brings. The core pages reach
/// everything specific to the ruleset through these members.
class Dnd5eUi extends GameSystemUi {
  const Dnd5eUi();

  @override
  String get id => 'dnd5e';

  @override
  String get name => 'D&D 5e (SRD 5.1)';

  @override
  List<SystemAttribution> get attributions => [
    SystemAttribution(
      id: 'srd',
      ruleset: 'SRD 5.1',
      text: dnd5eSrdAttribution,
      serverText: attributionTextProvider,
      licenseId: 'cc-by-4',
      licenseTitle: 'Licencia CC-BY 4.0',
      licenseAsset: 'assets/licenses/CC-BY-4.0.txt',
    ),
  ];

  @override
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey) =>
      dnd5eRoutes(rootNavigatorKey);

  // -- Sheet ------------------------------------------------------------------

  /// The full "Combate" view, or the player's own one in "Mi sesión".
  @override
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    bool inSession = false,
    Widget? header,
  }) => inSession
      ? Dnd5ePlayerCombat(character: character, header: header)
      : CombatView(character: character, canEdit: canEdit, isDm: isDm, header: header);

  @override
  List<SheetTab> detailTabs(
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
  }) => [
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
    SheetTab(
      id: 'traits',
      label: 'Rasgos',
      builder: (_) => TraitsTab(character: character),
    ),
    SheetTab(
      id: 'spells',
      label: 'Hechizos',
      builder: (_) => SpellsTab(character: character),
    ),
  ];

  @override
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character) =>
      character.pendingLevelUpTo == null ? null : LevelUpCard(character: character);

  /// One forced page at a time: the granted level, the invalid picks, the
  /// spell preparation and the rest rolls.
  @override
  String? pendingActionRoute(CharacterDetail character) {
    if (character.pendingLevelUpTo != null) return Dnd5eRoutes.levelUp(character.id);
    if (character.invalidChoices.isNotEmpty) return Dnd5eRoutes.invalidChoices(character.id);
    if (character.spellPreparationPending) return Dnd5eRoutes.prepareSpells(character.id);
    if (character.restRollsPending) return Dnd5eRoutes.restRolls(character.id);
    return null;
  }

  @override
  List<Widget> sessionCards(BuildContext context, CharacterDetail character) => [
    if (character.pendingLevelUpTo != null) LevelUpCard(character: character),
    RestSection(character: character, canEdit: true),
  ];

  @override
  bool isDown(CharacterDetail character) => character.hitPointsCurrent == 0;

  @override
  Widget characterAccent(CharacterDetail character, {required Widget child}) =>
      ClassAccent(classIndex: mainClassIndex(character), child: child);

  @override
  Widget? characterHeadline(CharacterDetail character, {bool compact = false}) {
    if (!compact) return Dnd5eCharacterHeadline(character: character);
    return character.classes.isEmpty ? null : Dnd5eCompactHeadline(character: character);
  }

  @override
  List<Widget> characterBadges(CharacterDetail character) => [
    if (character.missingContent.isNotEmpty) CatalogMissingBadge(character: character),
  ];

  @override
  Widget? notesFacts(CharacterDetail character) => Dnd5eNotesFacts(character: character);

  @override
  Widget? sheetEditorSection(SheetEditorScope scope) => SheetEditorForm(character: scope.character);

  @override
  Widget? heightWeightRoller(HeightWeightScope scope) => Dnd5eHeightWeightRoller(scope: scope);

  // -- Creation ---------------------------------------------------------------

  @override
  Future<void> openCreationWizard(BuildContext context, String campaignId, {String? ownerUserId}) =>
      context.push(Dnd5eRoutes.characterWizard(campaignId, ownerUserId: ownerUserId));

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
    CompendiumTab(id: 'rules', label: 'Reglas', builder: (_) => const CompendiumRulesTab()),
    CompendiumTab(id: 'tables', label: 'Tablas', builder: (_) => const CompendiumTablesTab()),
  ];

  @override
  Future<List<CatalogSource>> catalogSources(Ref ref, {String? campaignId}) =>
      ref.watch(catalogRepositoryProvider).sources(campaignId: campaignId);

  @override
  Future<void> openCatalogItem(BuildContext context, String itemId) =>
      context.push(Dnd5eRoutes.item(itemId));

  /// The rarity, when the item has one.
  @override
  List<String> itemSummaryFacts(ItemSummary item) => [
    if (item.rarity != null) rarityLabel(item.rarity),
  ];

  @override
  Widget itemExtras(EffectiveItem item) => Dnd5eItemExtras(item: item);

  @override
  List<ItemFact> itemFacts(EffectiveItem item) => dnd5eItemFacts(item);

  @override
  ItemModifiersView? itemModifiers(EffectiveItem item) => dnd5eItemModifiers(item);

  @override
  FutureProvider<ItemSummary> itemTemplate(String templateId) => itemDetailProvider(templateId);

  @override
  void onItemTemplateChanged(Ref ref, String templateId) =>
      ref.invalidate(itemDetailProvider(templateId));

  /// The 5e fields of the item form ([ItemFieldsForm], an [ItemFormReader]).
  @override
  Widget? itemFormSection(ItemFormScope scope) => ItemFieldsForm(
    key: scope.formKey,
    template: scope.template,
    templateMode: scope.templateMode,
  );

  @override
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false}) =>
      combat.isCombatUsable(item, hasCharges: hasCharges);

  @override
  bool canEquip(EffectiveItem item) => dnd5e_item.canEquip(item);

  @override
  int get maxAttunedItems => dnd5e_item.maxAttunedItems;

  @override
  bool requiresAttunement(EffectiveItem item) => item.requiresAttunement;

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

  @override
  ProviderListenable<List<TableCharacter>> tableCharacters(String campaignId) =>
      partyControllerProvider(campaignId).select(
        (party) => [for (final m in party.value ?? const <PartyMember>[]) (id: m.id, name: m.name)],
      );

  @override
  List<ProviderOrFamily> tableProviders(String campaignId) => [partyControllerProvider(campaignId)];

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

  /// "PG actuales / máximos", when the summary carries them.
  @override
  String? rosterStatus(CharacterSummary summary) {
    final line = Dnd5eRosterLine.of(summary);
    final max = line.hitPointsMax;
    return max == null ? null : 'PG ${line.hitPointsCurrent ?? max} / $max';
  }

  @override
  ChangeDetail? describeChangeRequest(ChangeRequest request) => describeDnd5eChange(request);

  @override
  String? staleScope(String campaignId) => PartyRepository.partyPath(campaignId);

  /// The wizard: its class colour and icon.
  @override
  SystemAccent? get sampleAccent {
    final wizard = classThemeOf('wizard');
    return SystemAccent(icon: wizard.icon, light: wizard.accentLight, dark: wizard.accentDark);
  }

  // -- Dice, money, realtime ----------------------------------------------------

  /// A natural 20 is a critical and a natural 1 a fumble.
  @override
  RollClass classifyRoll(DiceResult result) => result.isCritical
      ? RollClass.critical
      : result.isFumble
      ? RollClass.fumble
      : RollClass.normal;

  /// "12 gp 5 sp 3 cp" (copper pieces; signed).
  @override
  String formatMoney(int minorUnits) => money.formatMoney(minorUnits);

  /// "15 gp" / "5 sp" (copper pieces).
  @override
  String formatPrice(int minorUnits) => formatCostCp(minorUnits);

  /// "1.5" (gp) for a text field.
  @override
  String moneyInputText(int minorUnits) => copperToGoldText(minorUnits);

  /// "1.5" (gp) -> 150 (cp).
  @override
  int? parseMoney(String text, {bool allowNegative = false}) =>
      money.parseGoldToCp(text, allowNegative: allowNegative);

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
  /// the character (the banner of the player comes from [playerNotice]).
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

  /// "¡Puedes subir a nivel N!" for the owner of the character the DM granted
  /// a level to; "El DM ha declarado un descanso" for a party rest.
  @override
  Future<CampaignEventNotice?> playerNotice(
    UnknownCampaignEvent event, {
    required Future<CharacterDetail?> Function(String characterId) ownCharacter,
  }) async {
    switch (dnd5eEventOf(event)) {
      case LevelUpGranted(:final characterId?):
        final level = (await ownCharacter(characterId))?.pendingLevelUpTo;
        if (level == null) return null;
        return CampaignEventNotice(
          id: 'level-up',
          text: '¡Puedes subir a nivel $level!',
          openSession: true,
        );
      case PartyRest():
        return const CampaignEventNotice(
          id: 'rest',
          text: 'El DM ha declarado un descanso',
          icon: AppIcons.campfire,
        );
      default:
        return null;
    }
  }
}
