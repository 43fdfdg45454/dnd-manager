import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';

import '../../catalog/data/models.dart' show SpellDetail;
import '../models.dart';

/// Whether a spell has something to roll or announce in combat: an attack, a
/// saving throw, damage or healing.
bool isCombatSpell(SpellDetail spell) =>
    spell.attackType != null ||
    spell.dcAbility != null ||
    spell.damageAtSlotLevel.isNotEmpty ||
    spell.damageAtCharacterLevel.isNotEmpty ||
    spell.healAtSlotLevel.isNotEmpty;

/// Spells the character can cast now: cantrips, spells that are prepared (or
/// always prepared) and every spell of a class that does not prepare (known
/// spells: the class has no `preparedMax`).
bool isCastable(CharacterDetail character, CharacterSpell spell, int level) {
  if (level == 0 || spell.isPrepared || spell.alwaysPrepared) return true;
  final casting = character.sheet.spellcasting
      .where((s) => s.classIndex == spell.classIndex)
      .firstOrNull;
  return casting == null || casting.preparedMax == null;
}

/// Spellcasting entry of the spell's class, or the first one as a fallback.
Spellcasting? spellcastingFor(CharacterDetail character, CharacterSpell spell) {
  final all = character.sheet.spellcasting;
  return all.where((s) => s.classIndex == spell.classIndex).firstOrNull ?? all.firstOrNull;
}

/// The value of [table] at [level]: the entry of the highest level not above
/// it, or the lowest entry when [level] is below them all. Null when empty.
String? scaledValue(Map<int, String> table, int level) {
  if (table.isEmpty) return null;
  final levels = table.keys.toList()..sort();
  var chosen = levels.first;
  for (final l in levels) {
    if (l <= level) chosen = l;
  }
  return table[chosen];
}

/// Replaces `MOD` (the spellcasting ability modifier) in a healing formula:
/// "1d8 + MOD" with +3 -> "1d8+3".
String withModifier(String formula, int modifier) {
  final text = formula.replaceAll(RegExp(r'\s+'), '');
  return text.replaceAll(RegExp(r'\+?MOD', caseSensitive: false), bonusSuffix(modifier));
}

/// Slot levels the spell can be cast at: from its level up to 9, keeping those
/// the character has slots for (regular or pact). When it has none, just the
/// spell's own level.
List<int> castLevels(int spellLevel, List<SpellSlot> slots, SpellSlot? pact) {
  if (spellLevel <= 0) return const [0];
  final levels = <int>{
    for (final s in slots)
      if (s.level >= spellLevel && s.max > 0) s.level,
    if (pact != null && pact.level >= spellLevel) pact.level,
  }.toList()..sort();
  return levels.isEmpty ? [spellLevel] : levels;
}

/// The entry of the spell's damage table used at [castLevel] or
/// [characterLevel]: its dice (as written) and whether the table goes by
/// character level (cantrips). Null without damage.
({String dice, bool byCharacterLevel})? spellDamageSource(
  SpellDetail spell, {
  required int castLevel,
  required int characterLevel,
}) {
  if (spell.level == 0 && spell.damageAtCharacterLevel.isNotEmpty) {
    return (
      dice: scaledValue(spell.damageAtCharacterLevel, characterLevel)!,
      byCharacterLevel: true,
    );
  }
  final bySlot = scaledValue(spell.damageAtSlotLevel, castLevel);
  if (bySlot != null) return (dice: bySlot, byCharacterLevel: false);
  final byCharacter = scaledValue(spell.damageAtCharacterLevel, characterLevel);
  return byCharacter == null ? null : (dice: byCharacter, byCharacterLevel: true);
}

/// Damage dice of [spell] cast at [castLevel] (cantrips scale with
/// [characterLevel]); doubled dice on a critical. Null without damage.
String? spellDamageExpression(
  SpellDetail spell, {
  required int castLevel,
  required int characterLevel,
  bool critical = false,
}) {
  final dice = spellDamageSource(spell, castLevel: castLevel, characterLevel: characterLevel)?.dice;
  if (dice == null) return null;
  final text = dice.replaceAll(RegExp(r'\s+'), '');
  if (!critical) return text;
  return DiceExpression.tryParse(text)?.doubleDice().toString() ?? text;
}

/// Healing of [spell] cast at [castLevel] with the ability [modifier]. Null
/// when the spell does not heal.
String? spellHealExpression(SpellDetail spell, {required int castLevel, required int modifier}) {
  final formula = scaledValue(spell.healAtSlotLevel, castLevel);
  return formula == null ? null : withModifier(formula, modifier);
}
