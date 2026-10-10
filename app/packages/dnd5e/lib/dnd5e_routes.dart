import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';

import 'catalog/ui/beast_page.dart';
import 'catalog/ui/class_detail_page.dart';
import 'catalog/ui/item_detail_page.dart';
import 'catalog/ui/race_detail_page.dart';
import 'catalog/ui/rule_page.dart';
import 'catalog/ui/spell_detail_page.dart';
import 'characters/ui/invalid_choices_page.dart';
import 'characters/ui/level_up/level_up_page.dart';
import 'characters/ui/prepare_spells_page.dart';
import 'characters/ui/rest_rolls_page.dart';
import 'characters/ui/wizard/character_wizard_page.dart';

/// Routes of the D&D 5e module (`Dnd5eUi.routes`). Their paths are the ones
/// the app always had; the core router adds them next to its own.
abstract final class Dnd5eRoutes {
  static const characterWizardPath = '/campaigns/:id/characters/new';
  static const levelUpPath = '/characters/:id/level-up';
  static const prepareSpellsPath = '/characters/:id/prepare-spells';
  static const invalidChoicesPath = '/characters/:id/invalid-choices';
  static const restRollsPath = '/characters/:id/rest-rolls';
  static const spellDetail = '/compendium/spells/:index';
  static const itemDetail = '/compendium/items/:id';
  static const classDetail = '/compendium/classes/:index';
  static const raceDetail = '/compendium/races/:index';
  static const beastDetail = '/compendium/beasts/:index';
  static const ruleDetail = '/compendium/rules/:index';

  /// Creation wizard of a new character; DMs may preselect the owner.
  static String characterWizard(String campaignId, {String? ownerUserId}) =>
      '/campaigns/$campaignId/characters/new'
      '${ownerUserId == null ? '' : '?ownerUserId=${Uri.encodeQueryComponent(ownerUserId)}'}';

  /// Level-up wizard of a character with a level granted by the DM.
  static String levelUp(String id) => '/characters/$id/level-up';

  /// "Prepara tus conjuros" (forced while the preparation is pending).
  static String prepareSpells(String id) => '/characters/$id/prepare-spells';

  /// "Sustituye lo que ya no cumples" (forced while there are invalid picks).
  static String invalidChoices(String id) => '/characters/$id/invalid-choices';

  /// "Tira tus dados" (forced while a rest roll is pending).
  static String restRolls(String id) => '/characters/$id/rest-rolls';

  static String spell(String index) => '/compendium/spells/${Uri.encodeComponent(index)}';

  static String item(String id) => '/compendium/items/${Uri.encodeComponent(id)}';

  static String dndClass(String index) => '/compendium/classes/${Uri.encodeComponent(index)}';

  static String race(String index) => '/compendium/races/${Uri.encodeComponent(index)}';

  static String beast(String index) => '/compendium/beasts/${Uri.encodeComponent(index)}';

  static String rule(String index) => '/compendium/rules/${Uri.encodeComponent(index)}';
}

/// The [GoRoute]s of [Dnd5eRoutes].
List<RouteBase> dnd5eRoutes(GlobalKey<NavigatorState> rootNavigatorKey) => [
  GoRoute(
    path: Dnd5eRoutes.characterWizardPath,
    builder: (context, state) => CharacterWizardPage(
      campaignId: state.pathParameters['id']!,
      ownerUserId: state.uri.queryParameters['ownerUserId'],
    ),
  ),
  GoRoute(
    path: Dnd5eRoutes.levelUpPath,
    builder: (context, state) => LevelUpPage(characterId: state.pathParameters['id']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.prepareSpellsPath,
    builder: (context, state) => PrepareSpellsPage(characterId: state.pathParameters['id']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.invalidChoicesPath,
    builder: (context, state) => InvalidChoicesPage(characterId: state.pathParameters['id']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.restRollsPath,
    builder: (context, state) => RestRollsPage(characterId: state.pathParameters['id']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.spellDetail,
    builder: (context, state) => SpellDetailPage(index: state.pathParameters['index']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.itemDetail,
    builder: (context, state) => ItemDetailPage(id: state.pathParameters['id']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.classDetail,
    builder: (context, state) => ClassDetailPage(index: state.pathParameters['index']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.raceDetail,
    builder: (context, state) => RaceDetailPage(index: state.pathParameters['index']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.beastDetail,
    builder: (context, state) => BeastPage(index: state.pathParameters['index']!),
  ),
  GoRoute(
    path: Dnd5eRoutes.ruleDetail,
    builder: (context, state) => RulePage(index: state.pathParameters['index']!),
  ),
];
