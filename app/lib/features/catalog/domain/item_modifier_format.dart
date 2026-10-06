import '../../characters/domain/character_format.dart' show formatModifier, skillLabel, skillLabels;
import '../data/models.dart' show ItemModifier;
import 'catalog_format.dart' show abilityLabel;

/// Spanish names of the modifier kinds (item form and detail lines).
const itemModifierKindLabels = <String, String>{
  'AbilityBonus': 'Bonus a característica',
  'AbilitySet': 'Fijar característica',
  'SaveBonus': 'Bonus a salvación',
  'SkillBonus': 'Bonus a habilidad',
  'ArmorClassBonus': 'Bonus a CA',
  'AttackBonus': 'Bonus de ataque',
  'DamageBonus': 'Bonus de daño',
  'SpeedBonus': 'Velocidad',
  'HitPointsMaxBonus': 'PG máximos',
  'InitiativeBonus': 'Iniciativa',
};

String itemModifierKindLabel(String kind) => itemModifierKindLabels[kind] ?? kind;

/// The kind needs an ability target.
bool modifierTargetsAbility(String kind) => kind == 'AbilityBonus' || kind == 'AbilitySet';

/// The kind accepts an ability target (null = every save).
bool modifierTargetsSave(String kind) => kind == 'SaveBonus';

/// The kind accepts a skill target (null = every skill).
bool modifierTargetsSkill(String kind) => kind == 'SkillBonus';

/// The kind has a target of any sort (required or optional).
bool modifierHasTarget(String kind) =>
    modifierTargetsAbility(kind) || modifierTargetsSave(kind) || modifierTargetsSkill(kind);

/// Skill indexes in display order (the 18 SRD skills).
List<String> get modifierSkillIndexes => skillLabels.keys.toList();

/// A readable Spanish line: "+3 Destreza", "Fuerza 19", "+1 CA",
/// "+1 a todas las salvaciones", "+2 Sigilo".
String describeItemModifier(ItemModifier m) {
  final value = formatModifier(m.value);
  final target = m.target;
  switch (m.kind) {
    case 'AbilityBonus':
      return '$value ${target == null ? 'a una característica' : abilityLabel(target)}';
    case 'AbilitySet':
      return '${target == null ? 'Característica' : abilityLabel(target)} ${m.value}';
    case 'SaveBonus':
      return target == null
          ? '$value a todas las salvaciones'
          : '$value a la salvación de ${abilityLabel(target)}';
    case 'SkillBonus':
      return target == null ? '$value a todas las habilidades' : '$value ${skillLabel(target)}';
    case 'ArmorClassBonus':
      return '$value CA';
    case 'AttackBonus':
      return '$value a las tiradas de ataque';
    case 'DamageBonus':
      return '$value al daño';
    case 'SpeedBonus':
      return '$value pies de velocidad';
    case 'HitPointsMaxBonus':
      return '$value PG máximos';
    case 'InitiativeBonus':
      return '$value a la iniciativa';
    default:
      return '${m.kind} $value';
  }
}
