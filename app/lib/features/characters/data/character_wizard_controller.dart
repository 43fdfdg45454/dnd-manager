import 'dart:async';
import 'dart:math' as math;

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../catalog/data/catalog_repository.dart';
import '../../catalog/data/models.dart'
    show
        Background,
        ClassDetail,
        ClassLevel,
        EquipmentCategoryItem,
        RaceDetail,
        StartingEquipment,
        StartingEquipmentChoice,
        StartingGold,
        StartingItem,
        Subrace;
import '../../items/data/inventory_repository.dart';
import '../domain/character_format.dart';
import 'characters_controller.dart';
import 'characters_repository.dart';
import 'models.dart';

/// Ways of setting the six base ability scores.
enum AbilityMethod {
  pointBuy('Compra por puntos'),
  standardArray('Matriz estándar'),
  rolled('Tirada (4d6, descarta el menor)'),
  manual('Manual');

  const AbilityMethod(this.label);

  final String label;
}

/// The values of the standard array.
const standardArray = <int>[15, 14, 13, 12, 10, 8];

/// Range of a rolled score (4d6 dropping the lowest die).
const rollMin = 3;
const rollMax = 18;

/// Parses a typed roll total; null when it is not a whole number in 3..18.
int? parseRollScore(String text) {
  final value = int.tryParse(text.trim());
  return value != null && value >= rollMin && value <= rollMax ? value : null;
}

/// Steps of the creation wizard in order. [origin] only exists when the race,
/// subrace or background ask for decisions and [spells] only for classes that
/// cast at level 1.
enum WizardStep {
  name,
  race,
  classChoice,
  abilities,
  background,
  origin,
  equipment,
  spells,
  review,
}

/// Classes that prepare spells (maximum = ability modifier + level).
const _preparingClasses = {'cleric', 'druid', 'paladin', 'wizard'};

/// The wizard keeps a spellbook apart from the spells it prepares.
const wizardClassIndex = 'wizard';

/// Spells a level 1 wizard copies into the spellbook (PHB).
const wizardSpellbookSize = 6;

/// What the player answered to one origin choice: the picked indexes (or
/// texts), or a feat with the ability it raises.
class OriginAnswer {
  const OriginAnswer({this.picks = const [], this.feat, this.ability});

  final List<String> picks;
  final String? feat;
  final String? ability;

  bool get isEmpty => picks.every((p) => p.trim().isEmpty) && feat == null;

  OriginAnswer copyWith({List<String>? picks, Object? feat = _unset, Object? ability = _unset}) =>
      OriginAnswer(
        picks: picks ?? this.picks,
        feat: identical(feat, _unset) ? this.feat : feat as String?,
        ability: identical(ability, _unset) ? this.ability : ability as String?,
      );
}

const _unset = Object();

const _defaultPointBuy = <String, int>{
  'str': pointBuyMin,
  'dex': pointBuyMin,
  'con': pointBuyMin,
  'int': pointBuyMin,
  'wis': pointBuyMin,
  'cha': pointBuyMin,
};

const _defaultManual = <String, int>{
  'str': 10,
  'dex': 10,
  'con': 10,
  'int': 10,
  'wis': 10,
  'cha': 10,
};

/// One line of starting equipment.
typedef WizardEquipment = ({String templateId, String name, int qty});

/// How the character gets its starting equipment.
enum EquipmentMode { kit, gold }

/// Key of the pick [category] of option [option] of choice [choice].
String categoryPickKey(int choice, int option, int category) => '$choice-$option-$category';

/// Lower-case dash-separated skill index of a skill name ("Sleight of Hand" ->
/// "sleight-of-hand"); an index stays as it is.
String skillIndexOf(String name) => name.trim().toLowerCase().replaceAll(RegExp(r'\s+'), '-');

/// Everything the user has chosen so far plus the catalog details the
/// validation needs ([race], [classDetail], [background]).
class WizardState {
  const WizardState({
    this.step = 0,
    this.name = '',
    this.owner,
    this.raceIndex,
    this.subraceIndex,
    this.applyRacialBonuses = true,
    this.classIndex,
    this.subclassIndex,
    this.method = AbilityMethod.pointBuy,
    this.pointBuyScores = _defaultPointBuy,
    this.arrayScores = const {},
    this.rollInputs = const ['', '', '', '', '', ''],
    this.rollAssignment = const {},
    this.manualScores = _defaultManual,
    this.backgroundIndex,
    this.skills = const {},
    this.languages = const {},
    this.equipment = const [],
    this.equipmentMode = EquipmentMode.kit,
    this.equipmentOptions = const {},
    this.categoryPicks = const {},
    this.goldRoll,
    this.keepBackgroundEquipment = false,
    this.cantrips = const [],
    this.leveledSpells = const [],
    this.preparedSpells = const {},
    this.alignment,
    this.notes = '',
    this.race,
    this.classDetail,
    this.background,
    this.loadError,
    this.originPlan,
    this.originAnswers = const {},
    this.originLoading = false,
    this.originError,
  });

  /// Index in [steps].
  final int step;
  final String name;

  /// Null: the user themselves for a player (the owner is not sent), an NPC
  /// for a DM, who has no characters of their own. `(userId: null)` is an NPC.
  final ({String? userId})? owner;

  final String? raceIndex;
  final String? subraceIndex;
  final bool applyRacialBonuses;
  final String? classIndex;
  final String? subclassIndex;
  final AbilityMethod method;

  /// Scores per method; each method keeps its own so switching does not lose them.
  final Map<String, int> pointBuyScores;

  /// Standard array assignment: ability key -> value (missing = not assigned).
  final Map<String, int> arrayScores;

  /// Raw text of the six typed roll totals, in input order.
  final List<String> rollInputs;

  /// Rolled assignment: ability key -> slot of [rollValues] (each slot once).
  final Map<String, int> rollAssignment;
  final Map<String, int> manualScores;
  final String? backgroundIndex;

  /// Skill indexes chosen from the class list.
  final Set<String> skills;

  /// Language names (SRD spelling).
  final Set<String> languages;
  final List<WizardEquipment> equipment;

  /// Kit (class and background equipment) or starting gold.
  final EquipmentMode equipmentMode;

  /// Choice index (see [equipmentChoices]) -> selected option indexes.
  final Map<int, Set<int>> equipmentOptions;

  /// [categoryPickKey] -> items picked from that category.
  final Map<String, List<EquipmentCategoryItem>> categoryPicks;

  /// Result of the starting gold roll typed by the user.
  final int? goldRoll;

  /// In gold mode, keep the equipment of the background.
  final bool keepBackgroundEquipment;
  final List<CharacterSpell> cantrips;

  /// Leveled spells chosen (the spellbook, for a wizard).
  final List<CharacterSpell> leveledSpells;

  /// Spell indexes of [leveledSpells] a wizard prepares (empty for other classes).
  final Set<String> preparedSpells;
  final String? alignment;
  final String notes;

  // Catalog details of the current choices (null until loaded).
  final RaceDetail? race;
  final ClassDetail? classDetail;
  final Background? background;

  /// Spanish error of the last failed catalog load.
  final String? loadError;

  /// Decisions of the race, subrace and background as the server plans them
  /// for the draft (null until loaded).
  final OriginChoices? originPlan;

  /// Answers by choice key.
  final Map<String, OriginAnswer> originAnswers;
  final bool originLoading;

  /// Spanish error of the last failed load of [originPlan].
  final String? originError;

  List<CharacterSpell> get spells => [...cantrips, ...leveledSpells];

  // -- Derived: abilities ------------------------------------------------------

  /// Base scores of the chosen [method] (unassigned standard array entries are absent).
  Map<String, int> get abilities => switch (method) {
    AbilityMethod.pointBuy => pointBuyScores,
    AbilityMethod.standardArray => arrayScores,
    AbilityMethod.rolled => {
      for (final e in rollAssignment.entries)
        if (rollValues != null) e.key: rollValues![e.value],
    },
    AbilityMethod.manual => manualScores,
  };

  /// The six typed rolls sorted high to low, or null until all six are valid.
  List<int>? get rollValues {
    final parsed = [for (final t in rollInputs) parseRollScore(t)];
    if (parsed.any((v) => v == null)) return null;
    return [for (final v in parsed) v!]..sort((a, b) => b.compareTo(a));
  }

  /// Ability key -> racial bonus (race and subrace; empty when not applied).
  Map<String, int> get racialBonuses {
    if (!applyRacialBonuses) return const {};
    final bonuses = <String, int>{};
    for (final b in [...?race?.abilityBonuses, ...?subrace?.abilityBonuses]) {
      final key = abilityKeyOf(b.ability);
      bonuses[key] = (bonuses[key] ?? 0) + b.bonus;
    }
    return bonuses;
  }

  Subrace? get subrace {
    final index = subraceIndex;
    if (index == null) return null;
    return race?.subraces.where((s) => s.index == index).firstOrNull;
  }

  /// Base score plus racial bonus, or null while the base is unassigned.
  int? finalScore(String key) {
    final base = abilities[key];
    return base == null ? null : base + (racialBonuses[key] ?? 0);
  }

  int get pointBuySpent => pointBuyTotal(pointBuyScores.values);

  // -- Derived: class ----------------------------------------------------------

  ClassLevel? get _levelOne => classDetail?.levels.where((l) => l.level == 1).firstOrNull;

  bool get needsSubclass {
    final detail = classDetail;
    final index = classIndex;
    return detail != null &&
        index != null &&
        subclassUnlockLevel(index) <= 1 &&
        detail.subclasses.isNotEmpty;
  }

  /// Cantrips the class knows at level 1.
  int get maxCantrips => _levelOne?.cantripsKnown ?? 0;

  /// True when the class has spells to choose at level 1.
  bool get hasSpellStep {
    final level = _levelOne;
    final detail = classDetail;
    if (level == null || detail == null) return false;
    final casts =
        (level.cantripsKnown ?? 0) > 0 ||
        (level.spellsKnown ?? 0) > 0 ||
        level.spellSlots.any((s) => s > 0);
    return detail.isSpellcaster && casts;
  }

  /// True for a class that keeps a spellbook apart from its prepared spells.
  bool get hasSpellbook => hasSpellStep && classDetail?.index == wizardClassIndex;

  /// Spells prepared at level 1 by a preparing class: `max(1, modifier + level)`.
  int get maxPrepared {
    final detail = classDetail;
    if (!hasSpellStep || detail == null || !_preparingClasses.contains(detail.index)) return 0;
    final ability = detail.spellcastingAbility;
    final score = ability == null ? null : finalScore(abilityKeyOf(ability));
    final modifier = score == null ? 0 : ((score - 10) / 2).floor();
    return math.max(1, modifier + 1);
  }

  /// Leveled spells chosen at level 1: the spellbook of a wizard
  /// ([wizardSpellbookSize]), the class table, or for preparing classes
  /// `max(1, modifier + level)`.
  int get maxSpells {
    final level = _levelOne;
    final detail = classDetail;
    if (!hasSpellStep || level == null || detail == null) return 0;
    if (hasSpellbook) return wizardSpellbookSize;
    if (level.spellsKnown != null) return level.spellsKnown!;
    return maxPrepared;
  }

  /// Spells of the spellbook that are prepared (only the ones still chosen).
  Set<String> get preparedChosen => {
    for (final s in leveledSpells)
      if (preparedSpells.contains(s.spellIndex)) s.spellIndex,
  };

  // -- Derived: origin choices -----------------------------------------------------

  /// True when the race, subrace or background ask for something besides
  /// languages (which have their own step).
  bool get needsOriginStep =>
      (race?.choices.asksBesidesLanguages ?? false) ||
      (subrace?.choices.asksBesidesLanguages ?? false) ||
      (background?.choices.asksBesidesLanguages ?? false);

  /// Choices of the plan the wizard asks for (languages have their own step).
  List<OriginChoice> get originChoices => [
    for (final c in originPlan?.choices ?? const <OriginChoice>[])
      if (c.kind != OriginChoiceKind.language) c,
  ];

  OriginAnswer originAnswerOf(OriginChoice choice) =>
      originAnswers[choice.key] ?? const OriginAnswer();

  /// Spanish error of [choice] while it lacks required picks, or null.
  String? originChoiceError(OriginChoice choice) {
    final answer = originAnswerOf(choice);
    if (choice.kind == OriginChoiceKind.feat) {
      if (choice.required == 0) return null;
      final feat = answer.feat == null ? null : choice.option(answer.feat!);
      if (feat == null) return '${choice.name}: elige una dote';
      final increase = feat.abilityIncrease;
      if (increase != null && increase.needsPick && answer.ability == null) {
        return '${choice.name}: elige la característica que sube ${feat.name}';
      }
      return null;
    }
    final picks = answer.picks.where((p) => p.trim().isNotEmpty).length;
    if (picks < choice.required) {
      final missing = choice.required - picks;
      return missing == 1
          ? '${choice.name}: falta 1 elección'
          : '${choice.name}: faltan $missing elecciones';
    }
    return null;
  }

  /// First error of the origin choices, or null when every required one is answered.
  String? get originValidation {
    if (originLoading) return 'Cargando las elecciones…';
    if (originError != null) return originError;
    if (originPlan == null) return 'Cargando las elecciones…';
    for (final c in originChoices) {
      final error = originChoiceError(c);
      if (error != null) return error;
    }
    return null;
  }

  /// Skills granted by the background, as skill indexes.
  Set<String> get backgroundSkills => {
    for (final s in background?.skillProficiencies ?? const <String>[]) skillIndexOf(s),
  };

  // -- Derived: starting equipment ------------------------------------------------

  StartingEquipment? get classEquipment => classDetail?.startingEquipment;
  StartingEquipment? get backgroundEquipment => background?.startingEquipment;

  /// True when the class has structured equipment (otherwise the texts are shown).
  bool get hasStructuredEquipment => classEquipment != null || backgroundEquipment != null;

  /// Starting wealth of the class, when it offers it.
  StartingGold? get startingGold => classEquipment?.gold;

  int get _classChoiceCount => classEquipment?.choices.length ?? 0;

  /// Choices of the class followed by those of the background; the position is
  /// the choice index used by [equipmentOptions].
  List<StartingEquipmentChoice> get allEquipmentChoices => [
    ...?classEquipment?.choices,
    ...?backgroundEquipment?.choices,
  ];

  /// True when the background equipment is part of the character.
  bool get usesBackgroundEquipment => equipmentMode == EquipmentMode.kit || keepBackgroundEquipment;

  /// Indexes (in [allEquipmentChoices]) of the choices that apply to the current mode.
  List<int> get activeChoiceIndexes {
    final classCount = _classChoiceCount;
    final total = allEquipmentChoices.length;
    return [
      for (var i = 0; i < total; i++)
        if (i < classCount ? equipmentMode == EquipmentMode.kit : usesBackgroundEquipment) i,
    ];
  }

  /// True when [choice] has the right number of options and every category pick filled.
  bool isChoiceComplete(int choice) {
    final all = allEquipmentChoices;
    if (choice < 0 || choice >= all.length) return false;
    final def = all[choice];
    final selected = equipmentOptions[choice] ?? const <int>{};
    if (selected.length != def.choose) return false;
    for (final o in selected) {
      final categories = o < def.options.length ? def.options[o].categories : const [];
      for (var k = 0; k < categories.length; k++) {
        if ((categoryPicks[categoryPickKey(choice, o, k)]?.length ?? 0) != categories[k].choose) {
          return false;
        }
      }
    }
    return true;
  }

  int get completedChoices => activeChoiceIndexes.where(isChoiceComplete).length;

  /// Allowed range of the gold roll, or null when the dice are unknown.
  ({int min, int max})? get goldRange {
    final dice = startingGold?.parsed;
    return dice == null ? null : (min: dice.count, max: dice.count * dice.sides);
  }

  /// Spanish error of the equipment step, or null.
  String? get equipmentError {
    if (!hasStructuredEquipment) return null;
    if (equipmentMode == EquipmentMode.gold && startingGold != null) {
      final range = goldRange;
      final roll = goldRoll;
      if (roll == null) return 'Escribe el resultado de la tirada de oro';
      if (range != null && (roll < range.min || roll > range.max)) {
        return 'La tirada va de ${range.min} a ${range.max}';
      }
    }
    if (completedChoices != activeChoiceIndexes.length) {
      return 'Completa todas las elecciones de equipo';
    }
    return null;
  }

  /// Gold of the rolled starting wealth in copper (gold mode only).
  int get rolledCopper {
    final gold = startingGold;
    if (equipmentMode != EquipmentMode.gold || gold == null || goldRoll == null) return 0;
    return (goldRoll! * gold.multiplier * 100).toInt();
  }

  /// Copper the new character starts with: fixed money of the background plus
  /// the rolled wealth.
  int get startingCopper =>
      (usesBackgroundEquipment ? backgroundEquipment?.fixedGoldCp ?? 0 : 0) + rolledCopper;

  /// Item lines of the starting kit (fixed items plus chosen options and
  /// category items), merged by template; items without a template are skipped.
  List<WizardEquipment> get startingLines {
    final lines = <String, WizardEquipment>{};
    void add(String? templateId, String name, int qty) {
      if (templateId == null || qty <= 0) return;
      final old = lines[templateId];
      lines[templateId] = (templateId: templateId, name: name, qty: (old?.qty ?? 0) + qty);
    }

    if (equipmentMode == EquipmentMode.kit) {
      for (final i in classEquipment?.fixed ?? const <StartingItem>[]) {
        add(i.templateId, i.name, i.quantity);
      }
    }
    if (usesBackgroundEquipment) {
      for (final i in backgroundEquipment?.fixed ?? const <StartingItem>[]) {
        add(i.templateId, i.name, i.quantity);
      }
    }
    final all = allEquipmentChoices;
    for (final c in activeChoiceIndexes) {
      for (final o in [...?equipmentOptions[c]]..sort()) {
        if (o >= all[c].options.length) continue;
        final option = all[c].options[o];
        for (final i in option.items) {
          add(i.templateId, i.name, i.quantity);
        }
        for (var k = 0; k < option.categories.length; k++) {
          for (final i in categoryPicks[categoryPickKey(c, o, k)] ?? const []) {
            add(i.templateId, i.name, 1);
          }
        }
      }
    }
    return lines.values.toList();
  }

  /// Every line to add to the inventory: the kit followed by the free list.
  List<WizardEquipment> get allEquipment {
    final merged = <String, WizardEquipment>{};
    for (final line in [...startingLines, ...equipment]) {
      final old = merged[line.templateId];
      merged[line.templateId] = (
        templateId: line.templateId,
        name: line.name,
        qty: (old?.qty ?? 0) + line.qty,
      );
    }
    return merged.values.toList();
  }

  /// Visible steps ("Elecciones de raza y trasfondo" only when something is
  /// asked, "Hechizos" only when the class casts).
  List<WizardStep> get steps => [
    for (final s in WizardStep.values)
      if ((s != WizardStep.spells || hasSpellStep) && (s != WizardStep.origin || needsOriginStep))
        s,
  ];

  WizardStep get currentStep => steps[step.clamp(0, steps.length - 1)];

  WizardState copyWith({
    int? step,
    String? name,
    Object? owner = _unset,
    Object? raceIndex = _unset,
    Object? subraceIndex = _unset,
    bool? applyRacialBonuses,
    Object? classIndex = _unset,
    Object? subclassIndex = _unset,
    AbilityMethod? method,
    Map<String, int>? pointBuyScores,
    Map<String, int>? arrayScores,
    List<String>? rollInputs,
    Map<String, int>? rollAssignment,
    Map<String, int>? manualScores,
    Object? backgroundIndex = _unset,
    Set<String>? skills,
    Set<String>? languages,
    List<WizardEquipment>? equipment,
    EquipmentMode? equipmentMode,
    Map<int, Set<int>>? equipmentOptions,
    Map<String, List<EquipmentCategoryItem>>? categoryPicks,
    Object? goldRoll = _unset,
    bool? keepBackgroundEquipment,
    List<CharacterSpell>? cantrips,
    List<CharacterSpell>? leveledSpells,
    Set<String>? preparedSpells,
    Object? alignment = _unset,
    String? notes,
    Object? race = _unset,
    Object? classDetail = _unset,
    Object? background = _unset,
    Object? loadError = _unset,
    Object? originPlan = _unset,
    Map<String, OriginAnswer>? originAnswers,
    bool? originLoading,
    Object? originError = _unset,
  }) => WizardState(
    step: step ?? this.step,
    name: name ?? this.name,
    owner: identical(owner, _unset) ? this.owner : owner as ({String? userId})?,
    raceIndex: identical(raceIndex, _unset) ? this.raceIndex : raceIndex as String?,
    subraceIndex: identical(subraceIndex, _unset) ? this.subraceIndex : subraceIndex as String?,
    applyRacialBonuses: applyRacialBonuses ?? this.applyRacialBonuses,
    classIndex: identical(classIndex, _unset) ? this.classIndex : classIndex as String?,
    subclassIndex: identical(subclassIndex, _unset) ? this.subclassIndex : subclassIndex as String?,
    method: method ?? this.method,
    pointBuyScores: pointBuyScores ?? this.pointBuyScores,
    arrayScores: arrayScores ?? this.arrayScores,
    rollInputs: rollInputs ?? this.rollInputs,
    rollAssignment: rollAssignment ?? this.rollAssignment,
    manualScores: manualScores ?? this.manualScores,
    backgroundIndex: identical(backgroundIndex, _unset)
        ? this.backgroundIndex
        : backgroundIndex as String?,
    skills: skills ?? this.skills,
    languages: languages ?? this.languages,
    equipment: equipment ?? this.equipment,
    equipmentMode: equipmentMode ?? this.equipmentMode,
    equipmentOptions: equipmentOptions ?? this.equipmentOptions,
    categoryPicks: categoryPicks ?? this.categoryPicks,
    goldRoll: identical(goldRoll, _unset) ? this.goldRoll : goldRoll as int?,
    keepBackgroundEquipment: keepBackgroundEquipment ?? this.keepBackgroundEquipment,
    cantrips: cantrips ?? this.cantrips,
    leveledSpells: leveledSpells ?? this.leveledSpells,
    preparedSpells: preparedSpells ?? this.preparedSpells,
    alignment: identical(alignment, _unset) ? this.alignment : alignment as String?,
    notes: notes ?? this.notes,
    race: identical(race, _unset) ? this.race : race as RaceDetail?,
    classDetail: identical(classDetail, _unset) ? this.classDetail : classDetail as ClassDetail?,
    background: identical(background, _unset) ? this.background : background as Background?,
    loadError: identical(loadError, _unset) ? this.loadError : loadError as String?,
    originPlan: identical(originPlan, _unset) ? this.originPlan : originPlan as OriginChoices?,
    originAnswers: originAnswers ?? this.originAnswers,
    originLoading: originLoading ?? this.originLoading,
    originError: identical(originError, _unset) ? this.originError : originError as String?,
  );

  // -- Validation ----------------------------------------------------------------

  /// Spanish error blocking the step at index [step] of [steps], or null.
  String? validate(int step) {
    final steps = this.steps;
    if (step < 0 || step >= steps.length) return null;
    switch (steps[step]) {
      case WizardStep.name:
        final trimmed = name.trim();
        if (trimmed.isEmpty) return 'Introduce un nombre';
        if (trimmed.length > 100) return 'El nombre admite como máximo 100 caracteres';
        return null;
      case WizardStep.race:
        if (raceIndex == null) return 'Elige una raza';
        final race = this.race;
        if (race == null) return loadError ?? 'Cargando la raza…';
        if (race.subraces.isNotEmpty && subraceIndex == null) return 'Elige una subraza';
        return null;
      case WizardStep.classChoice:
        if (classIndex == null) return 'Elige una clase';
        if (classDetail == null) return loadError ?? 'Cargando la clase…';
        if (needsSubclass && subclassIndex == null) return 'Elige una subclase';
        return null;
      case WizardStep.abilities:
        return _validateAbilities();
      case WizardStep.background:
        final choices = classDetail?.skillChoices;
        if (choices != null && choices.choose > 0 && skills.length != choices.choose) {
          final n = choices.choose;
          return n == 1 ? 'Elige 1 habilidad' : 'Elige $n habilidades';
        }
        return null;
      case WizardStep.origin:
        return originValidation;
      case WizardStep.equipment:
        return equipmentError;
      case WizardStep.spells:
        if (cantrips.length > maxCantrips) {
          return maxCantrips == 1 ? 'Como máximo 1 truco' : 'Como máximo $maxCantrips trucos';
        }
        if (leveledSpells.length > maxSpells) {
          return maxSpells == 1 ? 'Como máximo 1 hechizo' : 'Como máximo $maxSpells hechizos';
        }
        if (hasSpellbook && preparedChosen.length > maxPrepared) {
          return maxPrepared == 1
              ? 'Prepara como máximo 1 hechizo'
              : 'Prepara como máximo $maxPrepared hechizos';
        }
        return null;
      case WizardStep.review:
        for (var i = 0; i < steps.length - 1; i++) {
          final error = validate(i);
          if (error != null) return error;
        }
        return null;
    }
  }

  String? _validateAbilities() {
    switch (method) {
      case AbilityMethod.pointBuy:
        if (pointBuySpent > pointBuyBudget) return 'Te has pasado de $pointBuyBudget puntos';
        return null;
      case AbilityMethod.standardArray:
        if (abilityKeys.any((k) => arrayScores[k] == null)) return 'Asigna las seis puntuaciones';
        return null;
      case AbilityMethod.rolled:
        if (rollValues == null) return 'Escribe las seis tiradas (3 a 18)';
        if (abilityKeys.any((k) => rollAssignment[k] == null)) {
          return 'Asigna las seis puntuaciones';
        }
        return null;
      case AbilityMethod.manual:
        if (abilityKeys.any((k) => (manualScores[k] ?? 0) < 1 || manualScores[k]! > 20)) {
          return 'Las puntuaciones van de 1 a 20';
        }
        return null;
    }
  }

  // -- Request -------------------------------------------------------------------

  /// The single sheet patch that fills in the new character.
  SheetPatch toPatch() {
    final classKey = classIndex!;
    final bgSkills = backgroundSkills;
    return SheetPatch(
      name: name.trim(),
      raceIndex: raceIndex,
      subraceIndex: subraceIndex,
      backgroundIndex: backgroundIndex,
      alignment: alignment,
      applyRacialBonuses: applyRacialBonuses,
      baseAbilities: {for (final k in abilityKeys) k: abilities[k] ?? 10},
      classes: [SheetPatchClass(classIndex: classKey, subclassIndex: subclassIndex, level: 1)],
      proficiencies: [
        // Saving throws of the class, as the full sheet editor marks them.
        for (final a
            in (classDetail?.savingThrows ?? const <String>[])
                .map(abilityKeyOf)
                .where(abilityKeys.contains))
          CharacterProficiency(
            type: ProficiencyType.savingThrow,
            key: a,
            source: ProficiencySource.classSource,
          ),
        for (final s in skills)
          CharacterProficiency(
            type: ProficiencyType.skill,
            key: s,
            source: ProficiencySource.classSource,
          ),
        for (final s in bgSkills)
          if (!skills.contains(s))
            CharacterProficiency(
              type: ProficiencyType.skill,
              key: s,
              source: ProficiencySource.background,
            ),
        for (final l in languages)
          CharacterProficiency(
            type: ProficiencyType.language,
            key: l,
            source: ProficiencySource.race,
          ),
      ],
      spells: [
        for (final s in cantrips)
          CharacterSpell(spellIndex: s.spellIndex, classIndex: classKey, isPrepared: true),
        for (final s in leveledSpells)
          CharacterSpell(
            spellIndex: s.spellIndex,
            classIndex: classKey,
            // A wizard keeps a spellbook and prepares some of its spells.
            isPrepared: !hasSpellbook || preparedSpells.contains(s.spellIndex),
          ),
      ],
      overrides: const [],
      notes: notes.trim().isEmpty ? null : notes.trim(),
      copperPieces: startingCopper > 0 ? startingCopper : null,
      // Going back to change the race may leave an earlier draft with a subrace
      // or background the character no longer has.
      clear: {
        if (subraceIndex == null) 'subraceIndex',
        if (backgroundIndex == null) 'backgroundIndex',
      },
    );
  }

  /// Answers to send to `PUT /origin-choices`: every choice the wizard asked
  /// for, with an empty answer for the ones left blank.
  List<LevelUpChoiceAnswer> toOriginAnswers() {
    final answers = <LevelUpChoiceAnswer>[];
    for (final choice in originChoices) {
      final answer = originAnswerOf(choice);
      if (choice.kind == OriginChoiceKind.feat) {
        final feat = answer.feat;
        if (feat == null) {
          answers.add(LevelUpChoiceAnswer.picks(choice.key, const []));
        } else {
          final increase = choice.option(feat)?.abilityIncrease;
          answers.add(
            LevelUpChoiceAnswer.feat(
              choice.key,
              feat,
              ability: increase == null
                  ? null
                  : answer.ability ?? (increase.needsPick ? null : increase.options.first),
            ),
          );
        }
        continue;
      }
      answers.add(
        LevelUpChoiceAnswer.picks(choice.key, [
          for (final p in answer.picks)
            if (p.trim().isNotEmpty) choice.freeText ? p.trim() : p,
        ]),
      );
    }
    return answers;
  }
}

/// Family argument: campaign and (for DMs) the preselected owner.
typedef WizardArgs = ({String campaignId, String? ownerUserId});

/// State of the character creation wizard of one campaign.
class CharacterWizardController extends Notifier<WizardState> {
  CharacterWizardController(this.args);

  final WizardArgs args;

  String get campaignId => args.campaignId;

  // Progress of a submission, so a retry after a failure resumes instead of
  // creating the character twice. The draft may already exist when the origin
  // step asks the server for the race and background decisions.
  String? _createdId;
  bool _sheetSaved = false;
  bool _originSaved = false;
  int _itemsAdded = 0;

  /// Bumped on each load of the origin plan so a late answer is dropped.
  int _originRequest = 0;

  @override
  WizardState build() {
    // Kept alive for [_ownerToSend]: whether the user is a DM decides the owner.
    ref.listen(campaignDetailControllerProvider(campaignId), (_, _) {});
    return WizardState(owner: args.ownerUserId == null ? null : (userId: args.ownerUserId));
  }

  CatalogRepository get _catalog => ref.read(catalogRepositoryProvider);

  CharactersRepository get _characters => ref.read(charactersRepositoryProvider);

  // -- Navigation ----------------------------------------------------------------

  void goTo(int step) {
    final last = state.steps.length - 1;
    state = state.copyWith(step: step.clamp(0, last));
    if (state.currentStep == WizardStep.origin) unawaited(loadOrigin());
  }

  void back() => goTo(state.step - 1);

  /// Moves to the next step when the current one is valid; returns its error otherwise.
  String? next() {
    final error = state.validate(state.step);
    if (error != null) return error;
    goTo(state.step + 1);
    return null;
  }

  /// Like [next], but leaving the origin step first saves its answers on the
  /// server. Returns the error that blocks the step, or null once moved on.
  Future<String?> advance() async {
    if (state.currentStep != WizardStep.origin) return next();
    final error = state.validate(state.step);
    if (error != null) return error;
    try {
      await _saveOrigin();
    } catch (e) {
      return problemDetail(e) ?? describeCharacterError(e);
    }
    if (!ref.mounted) return null;
    goTo(state.step + 1);
    return null;
  }

  /// Deletes the draft created for the origin choices (the wizard was left).
  Future<void> discardDraft() async {
    final id = _createdId;
    if (id == null) return;
    _createdId = null;
    try {
      await _characters.delete(id);
    } catch (_) {
      // The draft stays in the list of drafts; nothing else to do.
    }
  }

  String? validate(int step) => state.validate(step);

  /// True when anything was typed or chosen (to confirm before leaving).
  bool get isDirty {
    final s = state;
    return s.name.isNotEmpty ||
        s.raceIndex != null ||
        s.classIndex != null ||
        s.backgroundIndex != null ||
        s.equipment.isNotEmpty ||
        s.equipmentOptions.isNotEmpty ||
        s.notes.isNotEmpty ||
        s.alignment != null;
  }

  // -- Step 1: name --------------------------------------------------------------

  void setName(String value) => state = state.copyWith(name: value);

  void setAlignment(String? value) => state = state.copyWith(alignment: value);

  void setOwner(({String? userId})? owner) => state = state.copyWith(owner: owner);

  /// Owner sent on creation. A DM has no characters of their own: without a
  /// player chosen, theirs is an NPC (explicit `ownerUserId: null`).
  Future<({String? userId})?> _ownerToSend(WizardState s) async {
    final campaign = await ref.read(campaignDetailControllerProvider(campaignId).future);
    return campaign.myRole.isAtLeastDm ? (userId: s.owner?.userId) : s.owner;
  }

  void setNotes(String value) => state = state.copyWith(notes: value);

  // -- Step 2: race --------------------------------------------------------------

  Future<void> selectRace(String index) async {
    if (state.raceIndex == index && state.race != null) return;
    state = state.copyWith(
      raceIndex: index,
      subraceIndex: null,
      race: null,
      loadError: null,
      originPlan: null,
      originAnswers: const {},
    );
    try {
      final detail = await _catalog.raceDetail(index);
      if (!ref.mounted || state.raceIndex != index) return;
      state = state.copyWith(race: detail, languages: {...detail.languages}, loadError: null);
    } catch (_) {
      if (!ref.mounted || state.raceIndex != index) return;
      state = state.copyWith(loadError: 'No se pudo cargar la raza. Inténtalo de nuevo.');
    }
  }

  void selectSubrace(String? index) =>
      state = state.copyWith(subraceIndex: index, originPlan: null, originAnswers: const {});

  void setApplyRacialBonuses(bool value) => state = state.copyWith(applyRacialBonuses: value);

  // -- Step 3: class -------------------------------------------------------------

  Future<void> selectClass(String index) async {
    if (state.classIndex == index && state.classDetail != null) return;
    state = state.copyWith(
      classIndex: index,
      subclassIndex: null,
      classDetail: null,
      skills: const {},
      cantrips: const [],
      leveledSpells: const [],
      preparedSpells: const {},
      equipmentMode: EquipmentMode.kit,
      equipmentOptions: const {},
      categoryPicks: const {},
      goldRoll: null,
      loadError: null,
    );
    try {
      final detail = await _catalog.classDetail(index);
      if (!ref.mounted || state.classIndex != index) return;
      state = state.copyWith(classDetail: detail, loadError: null);
    } catch (_) {
      if (!ref.mounted || state.classIndex != index) return;
      state = state.copyWith(loadError: 'No se pudo cargar la clase. Inténtalo de nuevo.');
    }
  }

  void selectSubclass(String? index) => state = state.copyWith(subclassIndex: index);

  // -- Step 4: abilities ---------------------------------------------------------

  void setMethod(AbilityMethod method) => state = state.copyWith(method: method);

  /// Raises or lowers a point buy score by [delta]; false when it would leave
  /// the 8-15 range or exceed the budget.
  bool changePointBuy(String key, int delta) {
    final scores = state.pointBuyScores;
    final current = scores[key] ?? pointBuyMin;
    final target = current + delta;
    if (target < pointBuyMin || target > pointBuyMax) return false;
    final updated = {...scores, key: target};
    if (pointBuyTotal(updated.values) > pointBuyBudget) return false;
    state = state.copyWith(pointBuyScores: updated);
    return true;
  }

  /// Sets a score without guards (the validation reports an excess).
  void setPointBuyScore(String key, int value) =>
      state = state.copyWith(pointBuyScores: {...state.pointBuyScores, key: value});

  /// Assigns [value] of the standard array to [key]; the value leaves any
  /// other ability that held it. A null [value] clears the ability.
  void assignArray(String key, int? value) {
    final scores = {...state.arrayScores};
    if (value == null) {
      scores.remove(key);
    } else {
      scores.removeWhere((_, v) => v == value);
      scores[key] = value;
    }
    state = state.copyWith(arrayScores: scores);
  }

  /// Types roll total number [index]. Changing a total resets the assignment,
  /// since the sorted slots move.
  void setRollInput(int index, String text) {
    final inputs = [...state.rollInputs];
    inputs[index] = text;
    state = state.copyWith(rollInputs: inputs, rollAssignment: const {});
  }

  /// Assigns slot [slot] of the sorted rolls to [key]; the slot leaves any
  /// other ability that held it. A null [slot] clears the ability.
  void assignRoll(String key, int? slot) {
    final scores = {...state.rollAssignment};
    if (slot == null) {
      scores.remove(key);
    } else {
      scores.removeWhere((_, v) => v == slot);
      scores[key] = slot;
    }
    state = state.copyWith(rollAssignment: scores);
  }

  void setManualScore(String key, int value) =>
      state = state.copyWith(manualScores: {...state.manualScores, key: value});

  // -- Step 5: background and proficiencies ---------------------------------------

  void selectBackground(Background? background) {
    final granted = {
      for (final s in background?.skillProficiencies ?? const <String>[]) skillIndexOf(s),
    };
    // Selections of the previous background's choices are dropped; the class ones stay.
    final classCount =
        state.allEquipmentChoices.length - (state.backgroundEquipment?.choices.length ?? 0);
    state = state.copyWith(
      backgroundIndex: background?.index,
      background: background,
      originPlan: null,
      originAnswers: const {},
      keepBackgroundEquipment: false,
      equipmentOptions: {
        for (final e in state.equipmentOptions.entries)
          if (e.key < classCount) e.key: e.value,
      },
      categoryPicks: {
        for (final e in state.categoryPicks.entries)
          if (int.parse(e.key.split('-').first) < classCount) e.key: e.value,
      },
      skills: {
        for (final s in state.skills)
          if (!granted.contains(s)) s,
      },
    );
  }

  void toggleSkill(String index) {
    final choose = state.classDetail?.skillChoices.choose ?? 0;
    final skills = {...state.skills};
    if (!skills.remove(index)) {
      if (state.backgroundSkills.contains(index) || skills.length >= choose) return;
      skills.add(index);
    }
    state = state.copyWith(skills: skills);
  }

  void toggleLanguage(String language) {
    final languages = {...state.languages};
    if (!languages.remove(language)) languages.add(language);
    state = state.copyWith(languages: languages);
  }

  // -- Step 6: equipment ---------------------------------------------------------

  void setEquipmentMode(EquipmentMode mode) {
    if (mode == EquipmentMode.gold && state.startingGold == null) return;
    state = state.copyWith(equipmentMode: mode);
  }

  /// Selects option [option] of [choice]. With `choose` 1 it replaces the
  /// previous option; with more it toggles, up to the limit.
  void selectEquipmentOption(int choice, int option) {
    final all = state.allEquipmentChoices;
    if (choice < 0 || choice >= all.length) return;
    final choose = all[choice].choose;
    final current = {...?state.equipmentOptions[choice]};
    if (choose <= 1) {
      current
        ..clear()
        ..add(option);
    } else if (!current.remove(option)) {
      if (current.length >= choose) return;
      current.add(option);
    }
    state = state.copyWith(equipmentOptions: {...state.equipmentOptions, choice: current});
  }

  /// Toggles [item] in the category pick [key]; at the limit it replaces the
  /// pick when only one item is allowed and is ignored otherwise.
  void toggleCategoryItem(String key, int limit, EquipmentCategoryItem item) {
    final picks = [...?state.categoryPicks[key]];
    final i = picks.indexWhere((e) => e.templateId == item.templateId);
    if (i >= 0) {
      picks.removeAt(i);
    } else if (picks.length < limit) {
      picks.add(item);
    } else if (limit == 1) {
      picks
        ..clear()
        ..add(item);
    } else {
      return;
    }
    state = state.copyWith(categoryPicks: {...state.categoryPicks, key: picks});
  }

  void setGoldRoll(int? roll) => state = state.copyWith(goldRoll: roll);

  void setKeepBackgroundEquipment(bool value) =>
      state = state.copyWith(keepBackgroundEquipment: value);

  void addEquipment(String templateId, String name) {
    final lines = [...state.equipment];
    final i = lines.indexWhere((e) => e.templateId == templateId);
    if (i >= 0) {
      lines[i] = (templateId: templateId, name: name, qty: lines[i].qty + 1);
    } else {
      lines.add((templateId: templateId, name: name, qty: 1));
    }
    state = state.copyWith(equipment: lines);
  }

  void setEquipmentQuantity(String templateId, int qty) {
    state = state.copyWith(
      equipment: [
        for (final e in state.equipment)
          if (e.templateId != templateId)
            e
          else if (qty > 0)
            (templateId: e.templateId, name: e.name, qty: qty),
      ],
    );
  }

  void removeEquipment(String templateId) => setEquipmentQuantity(templateId, 0);

  // -- Step 7: spells ------------------------------------------------------------

  void addSpells(List<CharacterSpell> spells) {
    final cantrips = [...state.cantrips];
    final leveled = [...state.leveledSpells];
    // A wizard prepares the first spells it copies, up to its maximum.
    final prepared = {...state.preparedChosen};
    for (final s in spells) {
      final exists = [...cantrips, ...leveled].any((e) => e.spellIndex == s.spellIndex);
      if (exists) continue;
      if ((s.level ?? 1) == 0) {
        cantrips.add(s);
      } else {
        leveled.add(s);
        if (state.hasSpellbook && prepared.length < state.maxPrepared) prepared.add(s.spellIndex);
      }
    }
    state = state.copyWith(cantrips: cantrips, leveledSpells: leveled, preparedSpells: prepared);
  }

  /// Prepares or unprepares a spell of the wizard's spellbook; false when it
  /// would exceed the maximum.
  bool togglePrepared(String spellIndex) {
    final prepared = {...state.preparedChosen};
    if (!prepared.remove(spellIndex)) {
      if (prepared.length >= state.maxPrepared) return false;
      prepared.add(spellIndex);
    }
    state = state.copyWith(preparedSpells: prepared);
    return true;
  }

  void removeSpell(String spellIndex) => state = state.copyWith(
    preparedSpells: {...state.preparedSpells}..remove(spellIndex),
    cantrips: [
      for (final s in state.cantrips)
        if (s.spellIndex != spellIndex) s,
    ],
    leveledSpells: [
      for (final s in state.leveledSpells)
        if (s.spellIndex != spellIndex) s,
    ],
  );

  // -- Origin choices --------------------------------------------------------------

  /// Creates the draft on the server (once) and saves the identity of the
  /// character (name, race, background, abilities and class), which is what
  /// the planning of the race and background decisions needs.
  Future<String> _ensureDraft() async {
    final s = state;
    var id = _createdId;
    if (id == null) {
      final created = await _characters.create(
        campaignId,
        name: s.name.trim(),
        owner: await _ownerToSend(s),
      );
      id = created.id;
      _createdId = id;
    }
    await _characters.patchSheet(
      id,
      SheetPatch(
        name: s.name.trim(),
        raceIndex: s.raceIndex,
        subraceIndex: s.subraceIndex,
        backgroundIndex: s.backgroundIndex,
        applyRacialBonuses: s.applyRacialBonuses,
        baseAbilities: {for (final k in abilityKeys) k: s.abilities[k] ?? 10},
        classes: s.classIndex == null
            ? null
            : [
                SheetPatchClass(
                  classIndex: s.classIndex!,
                  subclassIndex: s.subclassIndex,
                  level: 1,
                ),
              ],
        clear: {
          if (s.subraceIndex == null) 'subraceIndex',
          if (s.backgroundIndex == null) 'backgroundIndex',
        },
      ),
    );
    return id;
  }

  /// Loads the decisions of the race, subrace and background of the draft. The
  /// answers typed before stay; the rest come from the server.
  Future<void> loadOrigin() async {
    final request = ++_originRequest;
    state = state.copyWith(originLoading: true, originError: null);
    try {
      final id = await _ensureDraft();
      final plan = await _characters.originChoices(id);
      if (!ref.mounted || request != _originRequest) return;
      final answers = <String, OriginAnswer>{};
      for (final c in plan.choices) {
        final typed = state.originAnswers[c.key];
        answers[c.key] =
            typed ??
            OriginAnswer(
              picks: [for (final i in c.selected) i.index],
              feat: c.feat?.index,
              ability: c.ability,
            );
      }
      state = state.copyWith(
        originPlan: plan,
        originAnswers: answers,
        originLoading: false,
        originError: null,
      );
    } catch (error) {
      if (!ref.mounted || request != _originRequest) return;
      state = state.copyWith(
        originLoading: false,
        originError: problemDetail(error) ?? describeCharacterError(error),
      );
    }
  }

  void _setAnswer(OriginChoice choice, OriginAnswer Function(OriginAnswer) change) {
    state = state.copyWith(
      originAnswers: {...state.originAnswers, choice.key: change(state.originAnswerOf(choice))},
    );
  }

  /// Picks or unpicks [index]; beyond [OriginChoice.choose] a single pick is
  /// replaced and further ones are ignored.
  void toggleOriginOption(OriginChoice choice, String index) {
    final option = choice.option(index);
    _setAnswer(choice, (a) {
      final picks = [...a.picks];
      if (picks.remove(index)) return a.copyWith(picks: picks);
      if (option != null && !option.eligible) return a;
      if (picks.length < choice.choose) return a.copyWith(picks: [...picks, index]);
      if (choice.choose == 1) return a.copyWith(picks: [index]);
      return a;
    });
  }

  /// Writes the [slot]-th value of a free-text choice (tools of any kind).
  void setOriginText(OriginChoice choice, int slot, String text) {
    _setAnswer(choice, (a) {
      final values = [...a.picks];
      while (values.length <= slot) {
        values.add('');
      }
      values[slot] = text;
      return a.copyWith(picks: values);
    });
  }

  void selectOriginFeat(OriginChoice choice, String index) {
    final option = choice.option(index);
    if (option == null || !option.eligible) return;
    _setAnswer(
      choice,
      (a) => a.feat == index
          ? a.copyWith(feat: null, ability: null)
          : a.copyWith(feat: index, ability: null),
    );
  }

  void setOriginFeatAbility(OriginChoice choice, String ability) =>
      _setAnswer(choice, (a) => a.copyWith(ability: ability));

  Future<void> _saveOrigin() async {
    final id = _createdId ?? await _ensureDraft();
    final answers = state.toOriginAnswers();
    final result = await _characters.saveOriginChoices(id, answers);
    if (ref.mounted) state = state.copyWith(originPlan: result);
  }

  // -- Submit --------------------------------------------------------------------

  /// Creates the character, fills in its sheet and adds the equipment lines.
  /// Returns the new character id. Errors are rethrown; calling it again resumes
  /// after the last completed request.
  Future<String> submit() async {
    final error = state.validate(state.steps.length - 1);
    if (error != null) throw StateError(error);
    final characters = ref.read(charactersRepositoryProvider);
    final inventory = ref.read(inventoryRepositoryProvider);

    final s = state;
    var id = _createdId;
    if (id == null) {
      final created = await characters.create(
        campaignId,
        name: s.name.trim(),
        owner: await _ownerToSend(s),
      );
      id = created.id;
      _createdId = id;
    }
    if (!_sheetSaved) {
      await characters.patchSheet(id, s.toPatch());
      _sheetSaved = true;
    }
    // The full sheet replaces the lists the origin choices add to: answer them again.
    if (!_originSaved && s.originChoices.isNotEmpty) {
      await characters.saveOriginChoices(id, s.toOriginAnswers());
      _originSaved = true;
    }
    final lines = s.allEquipment;
    while (_itemsAdded < lines.length) {
      final line = lines[_itemsAdded];
      await inventory.add(id, templateId: line.templateId, quantity: line.qty);
      _itemsAdded++;
    }
    ref.invalidate(campaignCharactersControllerProvider(campaignId));
    return id;
  }
}

final characterWizardControllerProvider = NotifierProvider.autoDispose
    .family<CharacterWizardController, WizardState, WizardArgs>(CharacterWizardController.new);
