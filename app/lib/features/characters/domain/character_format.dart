import '../../campaigns/domain/campaign_models.dart';
import '../data/models.dart';

/// Signed modifier: 3 -> "+3", 0 -> "+0", -1 -> "-1".
String formatModifier(int value) => value >= 0 ? '+$value' : '$value';

/// Three-letter Spanish abbreviation of an ability key ("str" -> "Fue").
String abilityAbbreviation(String key) => switch (key.toLowerCase()) {
  'str' => 'Fue',
  'dex' => 'Des',
  'con' => 'Con',
  'int' => 'Int',
  'wis' => 'Sab',
  'cha' => 'Car',
  _ => key,
};

/// Ability key ("str", ...) from a catalog value such as "str" or "Strength".
String abilityKeyOf(String ability) {
  final text = ability.toLowerCase();
  return text.length <= 3 ? text : text.substring(0, 3);
}

/// Spanish names of the 18 SRD skills, keyed by catalog index.
const skillLabels = <String, String>{
  'acrobatics': 'Acrobacias',
  'animal-handling': 'Trato con animales',
  'arcana': 'Arcanos',
  'athletics': 'Atletismo',
  'deception': 'Engaño',
  'history': 'Historia',
  'insight': 'Perspicacia',
  'intimidation': 'Intimidación',
  'investigation': 'Investigación',
  'medicine': 'Medicina',
  'nature': 'Naturaleza',
  'perception': 'Percepción',
  'performance': 'Interpretación',
  'persuasion': 'Persuasión',
  'religion': 'Religión',
  'sleight-of-hand': 'Juego de manos',
  'stealth': 'Sigilo',
  'survival': 'Supervivencia',
};

/// Ability of each SRD skill.
const skillAbilities = <String, String>{
  'acrobatics': 'dex',
  'animal-handling': 'wis',
  'arcana': 'int',
  'athletics': 'str',
  'deception': 'cha',
  'history': 'int',
  'insight': 'wis',
  'intimidation': 'cha',
  'investigation': 'int',
  'medicine': 'wis',
  'nature': 'int',
  'perception': 'wis',
  'performance': 'cha',
  'persuasion': 'cha',
  'religion': 'int',
  'sleight-of-hand': 'dex',
  'stealth': 'dex',
  'survival': 'wis',
};

/// Spanish name of a skill; falls back to [fallback] (the catalog name).
String skillLabel(String index, [String? fallback]) => skillLabels[index] ?? fallback ?? index;

/// Alignments as stored (SRD names) with their Spanish label.
const alignments = <String, String>{
  'Lawful Good': 'Legal bueno',
  'Neutral Good': 'Neutral bueno',
  'Chaotic Good': 'Caótico bueno',
  'Lawful Neutral': 'Legal neutral',
  'Neutral': 'Neutral',
  'Chaotic Neutral': 'Caótico neutral',
  'Lawful Evil': 'Legal malvado',
  'Neutral Evil': 'Neutral malvado',
  'Chaotic Evil': 'Caótico malvado',
};

String alignmentLabel(String alignment) => alignments[alignment] ?? alignment;

/// Longest text of a personality field (server limit).
const personalityTextMaxLength = 1000;

/// Longest background detail ("Especialidad: …", server limit).
const backgroundDetailMaxLength = 200;

/// Spanish label of the alignment of a background ideal ("Lawful" -> "Legal",
/// "Any" -> "Cualquiera"); full alignments and unknown texts as [alignmentLabel].
String idealAlignmentLabel(String alignment) =>
    const {
      'Lawful': 'Legal',
      'Chaotic': 'Caótico',
      'Good': 'Bueno',
      'Evil': 'Malvado',
      'Neutral': 'Neutral',
      'Any': 'Cualquiera',
    }[alignment] ??
    alignmentLabel(alignment);

/// Spanish label of an override field ("armorClass", "ability.str", ...).
String overrideFieldLabel(String field) {
  const fixed = {
    'hitPointsMax': 'Puntos de golpe máximos',
    'armorClass': 'Clase de armadura',
    'speed': 'Velocidad',
    'initiative': 'Iniciativa',
    'proficiencyBonus': 'Bonificador de competencia',
    'passivePerception': 'Percepción pasiva',
    'spellSaveDc': 'CD de conjuros',
    'spellAttackBonus': 'Ataque de conjuros',
  };
  final known = fixed[field];
  if (known != null) return known;
  final dot = field.indexOf('.');
  if (dot < 0) return field;
  final prefix = field.substring(0, dot);
  final rest = field.substring(dot + 1);
  return switch (prefix) {
    'ability' => 'Puntuación de ${_abilityName(rest)}',
    'save' => 'Salvación de ${_abilityName(rest)}',
    'skill' => 'Habilidad: ${skillLabel(rest)}',
    _ => field,
  };
}

/// Modifier of an ability score ((score - 10) / 2, rounded down).
int abilityModifierOf(int score) => ((score - 10) / 2).floor();

/// Spanish name of an ability key ("dex" -> "Destreza"); the key itself if unknown.
String abilityName(String key) => _abilityName(abilityKeyOf(key));

String _abilityName(String key) => switch (key) {
  'str' => 'Fuerza',
  'dex' => 'Destreza',
  'con' => 'Constitución',
  'int' => 'Inteligencia',
  'wis' => 'Sabiduría',
  'cha' => 'Carisma',
  _ => key,
};

/// Fields a [CharacterOverride] may target, in display order.
final List<String> overrideFields = [
  'hitPointsMax',
  'armorClass',
  'speed',
  'initiative',
  'proficiencyBonus',
  'passivePerception',
  'spellSaveDc',
  'spellAttackBonus',
  for (final a in abilityKeys) 'ability.$a',
  for (final a in abilityKeys) 'save.$a',
  for (final s in skillLabels.keys) 'skill.$s',
];

/// Level at which each SRD class picks its subclass (3 when unknown).
int subclassUnlockLevel(String classIndex) => switch (classIndex) {
  'cleric' || 'sorcerer' || 'warlock' => 1,
  'druid' || 'wizard' => 2,
  _ => 3,
};

// -- Point buy (standard 27 points) -----------------------------------------

const pointBuyBudget = 27;
const pointBuyMin = 8;
const pointBuyMax = 15;

/// Point cost of a score between 8 and 15.
int pointBuyCost(int score) => switch (score) {
  <= 8 => 0,
  9 => 1,
  10 => 2,
  11 => 3,
  12 => 4,
  13 => 5,
  14 => 7,
  _ => 9,
};

int pointBuyTotal(Iterable<int> scores) => scores.fold(0, (sum, s) => sum + pointBuyCost(s));

// -- Money --------------------------------------------------------------------

/// Gold text of an amount in copper: 1500 -> "15", 1550 -> "15.5".
String copperToGoldText(int copper) {
  final gold = copper / 100;
  if (copper % 100 == 0) return (copper ~/ 100).toString();
  final text = gold.toStringAsFixed(2);
  return text.endsWith('0') ? text.substring(0, text.length - 1) : text;
}

/// Parses a gold amount ("15", "15.5", "15,5") into copper; null if invalid or negative.
int? goldTextToCopper(String text) {
  final value = double.tryParse(text.trim().replaceAll(',', '.'));
  if (value == null || value < 0 || !value.isFinite) return null;
  return (value * 100).round();
}

// -- Permissions ----------------------------------------------------------------

/// What the signed-in user ([myUserId], [myRole] in the campaign) may do with a character.
class CharacterPermissions {
  const CharacterPermissions({
    required this.character,
    required this.myUserId,
    required this.myRole,
  });

  final CharacterDetail character;
  final String myUserId;

  /// Null while the campaign is still loading: treated as a plain player.
  final CampaignRole? myRole;

  bool get isDm => myRole?.isAtLeastDm ?? false;

  bool get isOwner => character.ownerUserId != null && character.ownerUserId == myUserId;

  bool get isDraft => character.status == CharacterStatus.draft;

  /// The DM edits directly in any state; the owner edits in Draft or sends a request.
  bool get canEdit => isDm || isOwner;

  /// Owner (not a DM, who can activate directly) of a Draft without a pending request.
  bool get canSubmit => isOwner && !isDm && isDraft && !character.hasPendingActivation;

  bool get canActivate => isDm && isDraft;

  bool get canDelete => isDm || (isOwner && isDraft);
}

/// Spanish names of the damage types.
const damageTypeLabels = <String, String>{
  'acid': 'ácido',
  'bludgeoning': 'contundente',
  'cold': 'frío',
  'fire': 'fuego',
  'force': 'fuerza',
  'lightning': 'relámpago',
  'necrotic': 'necrótico',
  'piercing': 'perforante',
  'poison': 'veneno',
  'psychic': 'psíquico',
  'radiant': 'radiante',
  'slashing': 'cortante',
  'thunder': 'trueno',
};

/// Spanish name of a damage type index ("fire" -> "fuego"); unknown ones stay as sent.
String damageTypeLabel(String damageType) =>
    damageTypeLabels[damageType.toLowerCase()] ?? damageType;
