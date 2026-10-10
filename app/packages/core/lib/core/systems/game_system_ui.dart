import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable, ProviderOrFamily;
import 'package:go_router/go_router.dart';

import '../../features/dice/domain/dice_expression.dart';
import '../../features/items/data/models.dart';
import '../catalog/catalog_models.dart';
import '../characters/change_detail.dart';
import '../characters/models.dart';
import '../realtime/realtime_events.dart';
import '../theme/icons.dart';
import '../ui/breakdown.dart';

/// One sub-tab of "Detalle" that a game system adds to the character page
/// (D&D 5e: Resumen, Habilidades, Rasgos, Hechizos). The core adds its own
/// (Inventario, Notas and, in "Mi sesión", Sesión).
@immutable
class SheetTab {
  const SheetTab({required this.id, required this.label, required this.builder});

  /// Stable id, remembered as the last sub-tab of the character
  /// (`characterTabProvider`) and used in the tab key (`tab-<id>`).
  final String id;

  /// Visible label ("Resumen").
  final String label;
  final WidgetBuilder builder;

  Key get tabKey => Key('tab-$id');
}

/// One tab of the compendium (D&D 5e: Hechizos, Objetos, Clases, Razas,
/// Bestias, Condiciones, Reglas, Tablas). Each tab reads the shared search
/// box and the scope of the page itself (`CompendiumScope`,
/// `compendiumSearchProvider`, `compendiumFilterProvider`).
@immutable
class CompendiumTab {
  const CompendiumTab({required this.id, required this.label, required this.builder});

  final String id;
  final String label;
  final WidgetBuilder builder;

  Key get tabKey => Key('tab-$id');
}

/// What a natural roll means for a game system.
enum RollClass { normal, critical, fumble }

/// Credits of the rules content a game system bundles (D&D 5e: the SRD 5.1
/// under CC-BY 4.0), listed on the attributions page.
@immutable
class SystemAttribution {
  const SystemAttribution({
    required this.id,
    required this.ruleset,
    required this.text,
    required this.licenseId,
    required this.licenseTitle,
    required this.licenseAsset,
    this.serverText,
  });

  /// Stable id of the credit (`srd`): the text has the key `attributions-<id>`.
  final String id;

  /// "SRD 5.1".
  final String ruleset;

  /// Attribution text shown when the server does not send its own.
  final String text;

  /// The text the server sends, when it has one (it wins over [text]).
  final ProviderListenable<AsyncValue<String>>? serverText;

  /// Stable id of the license (`cc-by-4`): its tile has the key
  /// `attributions-license-<licenseId>`.
  final String licenseId;

  /// "Licencia CC-BY 4.0".
  final String licenseTitle;

  /// Bundled license text (`assets/licenses/...`).
  final String licenseAsset;
}

/// What the manual sheet editor hands to the game system section.
@immutable
class SheetEditorScope {
  const SheetEditorScope({required this.character});

  final CharacterDetail character;
}

/// What the height and weight fields hand to the game system roller: the
/// title of the section, the race of the character being edited (it decides
/// the table, in D&D 5e), the key prefix of the fields and where to put the
/// result.
@immutable
class HeightWeightScope {
  const HeightWeightScope({
    required this.title,
    required this.keyPrefix,
    required this.onRolled,
    this.raceIndex,
    this.subraceIndex,
  });

  /// "Altura y peso": the roller lays it out beside its button.
  final Widget title;

  final String keyPrefix;
  final String? raceIndex;
  final String? subraceIndex;

  /// Called with the rolled height (inches) and weight (pounds).
  final void Function(int heightInches, int weightPounds) onRolled;
}

/// What an item form hands to the game system section: the form key the
/// parent validates and reads (its state is an [ItemFormReader]), the
/// template the form starts from and whether it edits a homebrew template
/// (with template-only fields).
@immutable
class ItemFormScope {
  const ItemFormScope({required this.formKey, this.template, this.templateMode = false});

  final GlobalKey formKey;

  /// The catalog template as [GameSystemUi.itemTemplate] loaded it, or null
  /// for an item from scratch.
  final ItemSummary? template;
  final bool templateMode;
}

/// What the core reads back from the item form of a game system: the state
/// of the widget [GameSystemUi.itemFormSection] puts under
/// [ItemFormScope.formKey].
abstract interface class ItemFormReader {
  /// Validates the fields; false shows their errors.
  bool validate();

  /// The homebrew template as the server takes it
  /// (`POST /api/v1/campaigns/{id}/items`).
  Map<String, dynamic> readTemplate();

  /// The fields that differ from [template] (the one of
  /// [ItemFormScope.template]); every field for an item from scratch.
  ItemOverrides readOverrides({ItemSummary? template});
}

/// One "label: value" row of the detail of an item; [fields] are the
/// override fields behind it (the row is marked when one of them changed).
typedef ItemFact = ({String label, String? value, List<String> fields});

/// The effects of an item the system lists in its detail ([view], null when
/// the item has none) and the override fields behind them.
typedef ItemModifiersView = ({Widget? view, List<String> fields});

/// A character at the DM table (stash, messages, dice).
typedef TableCharacter = ({String id, String name});

/// A notice a game system shows to the players for one of its campaign
/// events (D&D 5e: "¡Puedes subir a nivel N!", "El DM ha declarado un
/// descanso"). [id] makes the keys (`realtime-notice-<id>`, and
/// `realtime-notice-<id>-icon` for the [icon]); [openSession] adds "Ver",
/// which opens "Mi sesión".
@immutable
class CampaignEventNotice {
  const CampaignEventNotice({
    required this.id,
    required this.text,
    this.icon,
    this.openSession = false,
  });

  final String id;
  final String text;
  final AppIcons? icon;
  final bool openSession;
}

/// The accent a game system gives a sample character in the preview of
/// "Personalización" (D&D 5e: the wizard class colour and icon).
@immutable
class SystemAccent {
  const SystemAccent({required this.icon, required this.light, required this.dark});

  final AppIcons icon;
  final Color light;
  final Color dark;

  Color of(Brightness brightness) => brightness == Brightness.dark ? dark : light;
}

/// What a game system brings to the app. The core picks it by the
/// `systemId` of the campaign ([campaignSystemUiProvider]) and never imports
/// a game system directly: everything specific to a ruleset goes through
/// these members.
abstract class GameSystemUi {
  const GameSystemUi();

  /// Id of the system in the server (`dnd5e`).
  String get id;

  /// Display name ("D&D 5e (SRD 5.1)").
  String get name;

  /// Credits of the rules content of the system.
  List<SystemAttribution> get attributions;

  /// Own routes (level-up, spell preparation, choices, compendium details...).
  /// Their paths do not depend on the system registry.
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey);

  // -- Sheet ------------------------------------------------------------------

  /// The "Combate" view of a character. [inSession] is the player's own view
  /// in "Mi sesión" (what the system asks the player first, no rests: those
  /// live in the "Sesión" sub-tab).
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    bool inSession = false,
    Widget? header,
  });

  /// The sub-tabs of "Detalle" that belong to the system.
  List<SheetTab> detailTabs(CharacterDetail character, {required bool canEdit, bool isDm = false});

  /// A card with what the player must do before playing (a level granted,
  /// spells to prepare...), or null.
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character);

  /// A full-screen page the player must complete before playing (D&D 5e: the
  /// level-up wizard, the choices to replace, the spells to prepare, the rest
  /// rolls), or null. "Mi sesión" opens it by itself.
  String? pendingActionRoute(CharacterDetail character);

  /// The cards of the "Sesión" sub-tab of "Mi sesión" that belong to the
  /// system (D&D 5e: the level granted and the rests), above the shops, the
  /// stash and the messages of the core.
  List<Widget> sessionCards(BuildContext context, CharacterDetail character);

  /// Whether the character is down (D&D 5e: at 0 hit points): the session
  /// darkens its edges.
  bool isDown(CharacterDetail character);

  /// [child] with the accent of the character as `colorScheme.primary` (D&D
  /// 5e: the colour of its main class), for its header.
  Widget characterAccent(CharacterDetail character, {required Widget child});

  /// The line under the name of a character (D&D 5e: race, classes and level
  /// with the icon of the main class), or null. [compact]: the short line of
  /// "Mi sesión" (no race; null without classes).
  Widget? characterHeadline(CharacterDetail character, {bool compact = false});

  /// Chips of the system after the status of a character (D&D 5e:
  /// "Contenido no disponible").
  List<Widget> characterBadges(CharacterDetail character);

  /// Facts of the system on top of "Notas" (D&D 5e: background and
  /// alignment), or null.
  Widget? notesFacts(CharacterDetail character);

  /// The manual sheet editor of the system (a whole screen: the sheet of a
  /// game system is edited as one form), or null when it has none.
  Widget? sheetEditorSection(SheetEditorScope scope);

  /// The header of the height and weight fields with a button that rolls
  /// them with the tables of the system ([HeightWeightScope.title] beside
  /// it), or null when the system has none (the title is shown alone).
  Widget? heightWeightRoller(HeightWeightScope scope);

  // -- Creation ---------------------------------------------------------------

  /// Opens the creation wizard of a character of [campaignId]; DMs may
  /// preselect the owner.
  Future<void> openCreationWizard(BuildContext context, String campaignId, {String? ownerUserId});

  // -- Catalog and items --------------------------------------------------------

  /// The tabs of the compendium.
  List<CompendiumTab> compendiumTabs();

  /// The content packs and other sources of the catalog of the system, for
  /// the source chips and the source picker of the compendium. With
  /// [campaignId] each source says whether that campaign enables it. [ref] is
  /// the one of the provider that asks.
  Future<List<CatalogSource>> catalogSources(Ref ref, {String? campaignId});

  /// Opens the catalog page of the item template [itemId].
  Future<void> openCatalogItem(BuildContext context, String itemId);

  /// Facts of the system on a line of a catalog item, between its category
  /// and its price (D&D 5e: the rarity).
  List<String> itemSummaryFacts(ItemSummary item);

  /// Chips with the system facts of an item (damage, armor class, rarity,
  /// attunement...).
  Widget itemExtras(EffectiveItem item);

  /// The rows of the detail of an item after its quantity, charges and notes
  /// (D&D 5e: rarity, attunement, weight, damage, range, properties, armor).
  List<ItemFact> itemFacts(EffectiveItem item);

  /// The effects of the system an item applies (D&D 5e: its modifiers), or
  /// null when the system has no such effects.
  ItemModifiersView? itemModifiers(EffectiveItem item);

  /// The catalog template [templateId] the item forms start from (the value
  /// goes to [ItemFormScope.template]).
  FutureProvider<ItemSummary> itemTemplate(String templateId);

  /// The template [templateId] changed (a homebrew edit): reload what the
  /// system keeps of it.
  void onItemTemplateChanged(Ref ref, String templateId);

  /// The fields of an item form (its state is an [ItemFormReader]), or null
  /// when the system has no item form.
  Widget? itemFormSection(ItemFormScope scope);

  /// Whether [item] matters during a fight ([hasCharges]: an inventory item
  /// with charges).
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false});

  /// Whether the equip action makes sense for [item] (the server has the last
  /// word).
  bool canEquip(EffectiveItem item);

  /// How many items a character may be attuned to at once; 0 when the system
  /// has no attunement.
  int get maxAttunedItems;

  /// Whether [item] needs attunement to work.
  bool requiresAttunement(EffectiveItem item);

  /// Attunes [item] of [characterId], asking which item to drop when the limit
  /// is reached. Returns whether it was done.
  Future<bool> openAttunement(BuildContext context, String characterId, CharacterItem item);

  // -- Campaign -----------------------------------------------------------------

  /// The party actions and roster of the DM table.
  Widget partyPanel(BuildContext context, String campaignId);

  /// The characters at the DM table of [campaignId] (D&D 5e: the party).
  ProviderListenable<List<TableCharacter>> tableCharacters(String campaignId);

  /// What a pull to refresh of the DM table reloads (D&D 5e: the party).
  List<ProviderOrFamily> tableProviders(String campaignId);

  /// The roster line of a character ("Elfo · Mago 3").
  String rosterSubtitle(CharacterSummary summary);

  /// A short status at the end of the roster card (D&D 5e: "PG 7 / 12"), or
  /// null when the summary does not carry it (someone else's character).
  String? rosterStatus(CharacterSummary summary);

  /// The detail of the change requests the system understands (sheet edits,
  /// companion...), or null for the core ones.
  ChangeDetail? describeChangeRequest(ChangeRequest request);

  /// Cache path whose stale copy counts for the offline notice of the
  /// campaign [campaignId] (the party of the DM table), or null.
  String? staleScope(String campaignId);

  /// The accent of a sample character for the preview of "Personalización",
  /// or null for the palette accent.
  SystemAccent? get sampleAccent;

  // -- Dice, money, realtime ----------------------------------------------------

  /// What [result] means (critical, fumble or a normal roll).
  RollClass classifyRoll(DiceResult result);

  /// A balance in the smallest unit of the system as text, signed (D&D 5e:
  /// "1 pp 5 gp 5 sp").
  String formatMoney(int minorUnits);

  /// A price in the smallest unit of the system as it is quoted (D&D 5e:
  /// "1500 gp", "2 gp 5 sp").
  String formatPrice(int minorUnits);

  /// An amount as the money fields take it (D&D 5e: gold with decimals,
  /// "2.5").
  String moneyInputText(int minorUnits);

  /// An amount typed in a money field, in the smallest unit; null when it
  /// is not valid (or negative, unless [allowNegative]).
  int? parseMoney(String text, {bool allowNegative = false});

  /// Icon of a breakdown part the core does not know (D&D 5e: the class of
  /// a class or feature part), or null for the generic one.
  AppIcons? breakdownIcon(BreakdownPart part);

  /// A campaign event of the system (D&D 5e: `party.rest`,
  /// `levelUp.granted`): the system refreshes what it touched.
  void onRealtimeEvent(Ref ref, UnknownCampaignEvent event);

  /// The notice of a campaign event of the system for a player, or null.
  /// [ownCharacter] loads a character when it belongs to the user (null
  /// otherwise or when it cannot be loaded).
  Future<CampaignEventNotice?> playerNotice(
    UnknownCampaignEvent event, {
    required Future<CharacterDetail?> Function(String characterId) ownCharacter,
  });
}
