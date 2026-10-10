import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
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
/// Bestias, Condiciones, Tablas). Each tab reads the shared search box itself.
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
    required this.ruleset,
    required this.text,
    required this.licenseTitle,
    required this.licenseAsset,
  });

  /// "SRD 5.1".
  final String ruleset;

  /// Attribution text shown when the server does not send its own.
  final String text;

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
/// character being edited (its race decides the table, in D&D 5e), the key
/// prefix of the fields and where to put the result.
@immutable
class HeightWeightScope {
  const HeightWeightScope({
    required this.keyPrefix,
    required this.onRolled,
    this.raceIndex,
    this.subraceIndex,
  });

  final String keyPrefix;
  final String? raceIndex;
  final String? subraceIndex;

  /// Called with the rolled height (inches) and weight (pounds).
  final void Function(int heightInches, int weightPounds) onRolled;
}

/// What an item form hands to the game system section: the form key the
/// parent validates and reads, the initial item and whether it is a homebrew
/// template (with template-only fields).
@immutable
class ItemFormScope {
  const ItemFormScope({required this.formKey, required this.initial, this.templateMode = false});

  final GlobalKey formKey;

  /// The item as JSON (`ItemOverrides` or a catalog template), or empty.
  final Map<String, dynamic> initial;
  final bool templateMode;
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

  /// The "Combate" view of a character.
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    Widget? header,
  });

  /// The sub-tabs of "Detalle" that belong to the system.
  List<SheetTab> detailTabs(CharacterDetail character, {required bool canEdit, bool isDm = false});

  /// A card with what the player must do before playing (a level granted,
  /// spells to prepare...), or null.
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character);

  /// The part of the manual sheet editor that belongs to the system, or null.
  Widget? sheetEditorSection(SheetEditorScope scope);

  /// A button that rolls height and weight with the tables of the system, or
  /// null when it has none.
  Widget? heightWeightRoller(HeightWeightScope scope);

  // -- Creation ---------------------------------------------------------------

  /// Opens the creation wizard of a character of [campaignId]; DMs may
  /// preselect the owner.
  Future<void> openCreationWizard(BuildContext context, String campaignId, {String? ownerUserId});

  // -- Catalog and items --------------------------------------------------------

  /// The tabs of the compendium.
  List<CompendiumTab> compendiumTabs();

  /// The content packs and other sources of the catalog of the system, for
  /// the source chips. [ref] is the one of the provider that asks.
  Future<List<CatalogSource>> catalogSources(Ref ref);

  /// Chips with the system facts of an item (damage, armor class, rarity,
  /// attunement...).
  Widget itemExtras(EffectiveItem item);

  /// The fields of the system in an item form, or null.
  Widget? itemFormSection(ItemFormScope scope);

  /// Whether [item] matters during a fight ([hasCharges]: an inventory item
  /// with charges).
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false});

  /// Attunes [item] of [characterId], asking which item to drop when the limit
  /// is reached. Returns whether it was done.
  Future<bool> openAttunement(BuildContext context, String characterId, CharacterItem item);

  // -- Campaign -----------------------------------------------------------------

  /// The party actions and roster of the DM table.
  Widget partyPanel(BuildContext context, String campaignId);

  /// The roster line of a character ("Elfo · Mago 3").
  String rosterSubtitle(CharacterSummary summary);

  /// The detail of the change requests the system understands (sheet edits,
  /// companion...), or null for the core ones.
  ChangeDetail? describeChangeRequest(ChangeRequest request);

  /// Cache path whose stale copy counts for the offline notice of the
  /// campaign [campaignId] (the party of the DM table), or null.
  String? staleScope(String campaignId);

  // -- Dice, money, realtime ----------------------------------------------------

  /// What [result] means (critical, fumble or a normal roll).
  RollClass classifyRoll(DiceResult result);

  /// Money in the smallest unit of the system as text.
  String formatMoney(int minorUnits);

  /// Icon of a breakdown part the core does not know (D&D 5e: the class of
  /// a class or feature part), or null for the generic one.
  AppIcons? breakdownIcon(BreakdownPart part);

  /// A campaign event of the system (D&D 5e: `party.rest`,
  /// `levelUp.granted`): the system refreshes what it touched.
  void onRealtimeEvent(Ref ref, UnknownCampaignEvent event);
}
