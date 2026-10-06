import 'package:flutter/material.dart' show Icons;
import 'package:flutter/widgets.dart';

/// Icon set drawn for this project (project licence, see
/// `assets/icons/ATTRIBUTION.md`): 24×24 SVGs, 2 px stroke in `currentColor`,
/// "carved stone" style with straight segments and marked corners.
///
/// [fallback] is the Material icon shown when the asset cannot be loaded.
enum AppIcons {
  // Classes.
  barbarian('barbarian', Icons.sports_martial_arts),
  bard('bard', Icons.music_note),
  cleric('cleric', Icons.church),
  druid('druid', Icons.eco),
  fighter('fighter', Icons.shield),
  monk('monk', Icons.self_improvement),
  paladin('paladin', Icons.security),
  ranger('ranger', Icons.park),
  rogue('rogue', Icons.visibility_off),
  sorcerer('sorcerer', Icons.local_fire_department),
  warlock('warlock', Icons.remove_red_eye),
  wizard('wizard', Icons.auto_fix_high),
  // UI glyphs.
  d20('d20', Icons.casino),
  heart('heart', Icons.favorite),
  shield('shield', Icons.shield_outlined),
  scroll('scroll', Icons.description),
  potion('potion', Icons.science),
  campfire('campfire', Icons.local_fire_department_outlined),
  moon('moon', Icons.nightlight),
  sun('sun', Icons.wb_sunny),
  anvil('anvil', Icons.hardware),
  sword('sword', Icons.gavel),
  spellbook('spellbook', Icons.menu_book),
  map('map', Icons.map),
  castle('castle', Icons.castle),
  treasure('treasure', Icons.inventory_2),
  skull('skull', Icons.dangerous),
  envelope('envelope', Icons.mail),
  crown('crown', Icons.workspace_premium),
  hood('hood', Icons.person),
  compass('compass', Icons.explore),
  sparkles('sparkles', Icons.auto_awesome),
  backpack('backpack', Icons.backpack),
  coins('coins', Icons.monetization_on),
  calendar('calendar', Icons.calendar_month),
  book('book', Icons.book),
  quill('quill', Icons.edit),
  users('users', Icons.groups),
  // Combat and resources.
  bow('bow', Icons.north_east),
  staff('staff', Icons.auto_fix_normal),
  flame('flame', Icons.whatshot),
  bolt('bolt', Icons.bolt),
  eye('eye', Icons.visibility),

  /// Conditions.
  chains('chains', Icons.link),

  /// Live connection.
  seal('seal', Icons.verified),
  rune('rune', Icons.token),
  levelUp('levelUp', Icons.arrow_upward),

  /// Healing.
  drop('drop', Icons.water_drop),

  /// Damage.
  splash('splash', Icons.bloodtype),
  gear('gear', Icons.settings),

  /// Concentration.
  anchor('anchor', Icons.anchor),
  clock('clock', Icons.schedule);

  const AppIcons(this.file, this.fallback);

  /// File name without extension (not `name`: that is the enum's own getter).
  final String file;

  /// Material icon used when the SVG is unavailable.
  final IconData fallback;

  /// Asset path declared in `pubspec.yaml`.
  String get assetPath => 'assets/icons/$file.svg';
}
