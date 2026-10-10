import 'package:dio/dio.dart' show DioException;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/features/characters/data/characters_controller.dart';
import 'package:opentrpg_core/features/characters/data/characters_repository.dart';

import '../../catalog/domain/catalog_format.dart' show abilityLabel;
import '../dnd5e_characters_repository.dart';
import '../models.dart';

/// Highest score an Ability Score Improvement (or a feat) can reach.
const improvementMaxScore = 20;

/// Points of an Ability Score Improvement (+2 to one ability or +1 to two).
const improvementPoints = 2;

/// Kind of page of the level-up wizard.
enum LevelUpStepKind { classChoice, hitPoints, choice, review }

/// One page of the level-up wizard: the class, the hit points, one per choice
/// ([choiceKey]) and the review.
class LevelUpStep {
  const LevelUpStep._(this.kind, [this.choiceKey]);

  const LevelUpStep.choice(String key) : this._(LevelUpStepKind.choice, key);

  static const classChoice = LevelUpStep._(LevelUpStepKind.classChoice);
  static const hitPoints = LevelUpStep._(LevelUpStepKind.hitPoints);
  static const review = LevelUpStep._(LevelUpStepKind.review);

  final LevelUpStepKind kind;
  final String? choiceKey;

  /// "class", "hp", "choice-`key`" or "review" (used in widget keys).
  String get id => switch (kind) {
    LevelUpStepKind.classChoice => 'class',
    LevelUpStepKind.hitPoints => 'hp',
    LevelUpStepKind.choice => 'choice-$choiceKey',
    LevelUpStepKind.review => 'review',
  };

  @override
  bool operator ==(Object other) =>
      other is LevelUpStep && other.kind == kind && other.choiceKey == choiceKey;

  @override
  int get hashCode => Object.hash(kind, choiceKey);
}

/// Tab of an `AsiOrFeat` choice.
enum ImprovementMode { asi, feat }

/// What the player has picked for one choice so far.
class LevelUpSelection {
  const LevelUpSelection({
    this.selected = const [],
    this.replaced,
    this.mode = ImprovementMode.asi,
    this.asi = const {},
    this.feat,
    this.featAbility,
  });

  /// Option indexes (or texts for free-text choices), in pick order.
  final List<String> selected;

  /// Known pick swapped out (choices that allow replacements).
  final String? replaced;
  final ImprovementMode mode;

  /// Ability key -> points, for the "Mejora" tab.
  final Map<String, int> asi;
  final String? feat;

  /// Ability the [feat] raises, when the feat lets the player pick it.
  final String? featAbility;

  int get asiTotal => asi.values.fold(0, (sum, v) => sum + v);

  bool get isEmpty =>
      selected.every((s) => s.trim().isEmpty) && replaced == null && asiTotal == 0 && feat == null;

  LevelUpSelection copyWith({
    List<String>? selected,
    Object? replaced = _unset,
    ImprovementMode? mode,
    Map<String, int>? asi,
    Object? feat = _unset,
    Object? featAbility = _unset,
  }) => LevelUpSelection(
    selected: selected ?? this.selected,
    replaced: replaced == _unset ? this.replaced : replaced as String?,
    mode: mode ?? this.mode,
    asi: asi ?? this.asi,
    feat: feat == _unset ? this.feat : feat as String?,
    featAbility: featAbility == _unset ? this.featAbility : featAbility as String?,
  );
}

const _unset = Object();

/// State of the level-up wizard of one character.
class LevelUpState {
  const LevelUpState({
    this.classIndex,
    this.plan,
    this.loading = true,
    this.loadError,
    this.character,
    this.step = 0,
    this.hitPointsText = '',
    this.selections = const {},
    this.submitting = false,
    this.submitError,
    this.completed,
  });

  /// Class asked for (null until the first plan answers with the main class).
  final String? classIndex;

  /// Last plan received; kept while another class is loading.
  final LevelUpPlan? plan;
  final bool loading;
  final String? loadError;

  /// The character before the level-up (ability scores for the ASI caps).
  final CharacterDetail? character;
  final int step;
  final String hitPointsText;

  /// Selections by choice key.
  final Map<String, LevelUpSelection> selections;
  final bool submitting;
  final String? submitError;

  /// The updated character once the level-up has been applied.
  final CharacterDetail? completed;

  LevelUpState copyWith({
    Object? classIndex = _unset,
    Object? plan = _unset,
    bool? loading,
    Object? loadError = _unset,
    Object? character = _unset,
    int? step,
    String? hitPointsText,
    Map<String, LevelUpSelection>? selections,
    bool? submitting,
    Object? submitError = _unset,
    Object? completed = _unset,
  }) => LevelUpState(
    classIndex: classIndex == _unset ? this.classIndex : classIndex as String?,
    plan: plan == _unset ? this.plan : plan as LevelUpPlan?,
    loading: loading ?? this.loading,
    loadError: loadError == _unset ? this.loadError : loadError as String?,
    character: character == _unset ? this.character : character as CharacterDetail?,
    step: step ?? this.step,
    hitPointsText: hitPointsText ?? this.hitPointsText,
    selections: selections ?? this.selections,
    submitting: submitting ?? this.submitting,
    submitError: submitError == _unset ? this.submitError : submitError as String?,
    completed: completed == _unset ? this.completed : completed as CharacterDetail?,
  );

  LevelUpSelection selectionOf(String key) => selections[key] ?? const LevelUpSelection();

  // -- Class and subclass ------------------------------------------------------

  /// The class shown as selected: the one asked for, else the plan's.
  String? get selectedClassIndex => classIndex ?? plan?.classIndex;

  /// The plan belongs to the selected class (not a stale one being replaced).
  bool get planIsCurrent => plan != null && !loading && plan!.classIndex == selectedClassIndex;

  /// Subclass picked in this level-up, or the one the class already has.
  String? get subclassIndex {
    final p = plan;
    if (p == null) return null;
    for (final choice in p.choices) {
      if (choice.kind == LevelChoiceKind.subclass && choice.subclassIndex == null) {
        final picked = selectionOf(choice.key).selected;
        if (picked.isNotEmpty) return picked.first;
      }
    }
    return p.selectedClass?.subclassIndex;
  }

  /// Choices that apply: the general ones and those of the chosen subclass.
  List<LevelUpChoice> get visibleChoices {
    final p = plan;
    if (p == null) return const [];
    final subclass = subclassIndex;
    return [
      for (final c in p.choices)
        if (c.subclassIndex == null || c.subclassIndex == subclass) c,
    ];
  }

  /// Automatic features of the class level and of the chosen subclass.
  List<LevelUpFeature> get visibleFeatures {
    final p = plan;
    if (p == null) return const [];
    final subclass = subclassIndex;
    return [
      for (final f in p.automaticFeatures)
        if (f.subclassIndex == null || f.subclassIndex == subclass) f,
    ];
  }

  LevelUpChoice? choiceOf(String key) {
    for (final c in visibleChoices) {
      if (c.key == key) return c;
    }
    return null;
  }

  List<LevelUpStep> get steps => [
    LevelUpStep.classChoice,
    LevelUpStep.hitPoints,
    for (final c in visibleChoices) LevelUpStep.choice(c.key),
    LevelUpStep.review,
  ];

  LevelUpStep get currentStep {
    final all = steps;
    return all[step.clamp(0, all.length - 1)];
  }

  // -- Hit points --------------------------------------------------------------

  int get hitDie => plan?.hitDie ?? 8;

  /// The die result written, or null when it is not a number in 1..[hitDie].
  int? get hitPointsRolled {
    final value = int.tryParse(hitPointsText.trim());
    if (value == null || value < 1 || value > hitDie) return null;
    return value;
  }

  /// Hit points gained: the roll plus Constitution, at least 1.
  int? get hitPointsGained {
    final rolled = hitPointsRolled;
    if (rolled == null) return null;
    final total = rolled + (plan?.conModifier ?? 0);
    return total < 1 ? 1 : total;
  }

  // -- Abilities ---------------------------------------------------------------

  /// Score of [ability] without items and overrides (what the 20 cap is
  /// checked against).
  int naturalScore(String ability) {
    final sheet = character?.sheet;
    if (sheet == null) return 10;
    final breakdown = sheet.breakdown('ability.$ability');
    if (breakdown != null && breakdown.parts.isNotEmpty) {
      return breakdown.parts
          .where((p) => p.source != 'item' && p.source != 'override')
          .fold(0, (sum, p) => sum + p.value);
    }
    return sheet.ability(ability).score;
  }

  // -- Validation --------------------------------------------------------------

  /// Whether [option] applies with the current answers: options that depend
  /// on another pick of this level-up (`requires`) only when it is picked.
  bool isAvailable(LevelUpOption option) {
    final requires = option.requires;
    if (requires == null) return true;
    final selection = selectionOf(requires.choiceKey);
    return selection.selected.contains(requires.index) || selection.feat == requires.index;
  }

  /// Options of [choice] that apply with the current answers.
  List<LevelUpOption> availableOptions(LevelUpChoice choice) => [
    for (final o in choice.options)
      if (isAvailable(o)) o,
  ];

  /// Picks needed for [choice] with the current replacement. When options
  /// depend on other picks, no more than the eligible ones that apply.
  int neededPicks(LevelUpChoice choice) {
    var needed = choice.required;
    if (choice.options.any((o) => o.requires != null)) {
      final available = choice.options.where((o) => o.eligible && isAvailable(o)).length;
      if (available < needed) needed = available;
    }
    return needed + (selectionOf(choice.key).replaced == null ? 0 : 1);
  }

  /// Error of [choice], in Spanish, or null when its answer is complete.
  String? validateChoice(LevelUpChoice choice) {
    final selection = selectionOf(choice.key);
    if (choice.kind == LevelChoiceKind.asiOrFeat) return _validateImprovement(choice, selection);
    final picks = choice.freeText
        ? selection.selected.where((s) => s.trim().isNotEmpty).length
        : selection.selected.length;
    final needed = neededPicks(choice);
    if (picks != needed) {
      if (choice.freeText) {
        return needed == 1 ? 'Escribe 1 valor' : 'Escribe $needed valores';
      }
      return needed == 1 ? 'Elige 1 opción' : 'Elige $needed opciones';
    }
    if (!choice.freeText) {
      for (final index in selection.selected) {
        final option = choice.option(index);
        if (option != null && !option.eligible) {
          return '${option.name}: ${option.reason ?? 'no cumples los requisitos'}';
        }
        if (option != null && !isAvailable(option)) {
          return '${option.name}: depende de otra elección de este nivel';
        }
      }
    }
    return null;
  }

  /// The feat tab is the one that applies: a replacement of an invalid feat
  /// has no ability score improvement.
  bool _featMode(LevelUpChoice choice, LevelUpSelection selection) =>
      choice.isReplacement || selection.mode == ImprovementMode.feat;

  String? _validateImprovement(LevelUpChoice choice, LevelUpSelection selection) {
    if (choice.required == 0 && selection.isEmpty) return null;
    if (!_featMode(choice, selection)) {
      final values = selection.asi.values.where((v) => v != 0);
      final overCap = selection.asi.entries.any(
        (e) => e.value > 0 && naturalScore(e.key) + e.value > improvementMaxScore,
      );
      if (selection.asiTotal != improvementPoints || values.any((v) => v < 0) || overCap) {
        return 'Reparte $improvementPoints puntos (ninguna puede pasar de $improvementMaxScore)';
      }
      return null;
    }
    final feat = selection.feat == null ? null : choice.option(selection.feat!);
    if (feat == null) return 'Elige una dote';
    if (!feat.eligible) return '${feat.name}: ${feat.reason ?? 'no cumples los requisitos'}';
    final increase = feat.abilityIncrease;
    if (increase != null) {
      final ability = selection.featAbility ?? (increase.needsPick ? null : increase.options.first);
      if (ability == null) return 'Elige la característica que sube ${feat.name}';
      if (naturalScore(ability) + increase.amount > improvementMaxScore) {
        return '${feat.name} no puede subir ${abilityLabel(ability)} por encima de '
            '$improvementMaxScore';
      }
    }
    return null;
  }

  /// Error of the page [step] (an index of [steps]), or null when valid.
  String? validate(int step) {
    final all = steps;
    if (step < 0 || step >= all.length) return null;
    final target = all[step];
    switch (target.kind) {
      case LevelUpStepKind.classChoice:
        if (!planIsCurrent) return loadError ?? 'Cargando el plan de subida…';
        final selected = plan!.selectedClass;
        if (selected != null && !selected.allowed) {
          return selected.reason ?? 'No puedes subir de nivel en esta clase.';
        }
        return null;
      case LevelUpStepKind.hitPoints:
        return hitPointsRolled == null ? 'Escribe el resultado del dado (1-$hitDie)' : null;
      case LevelUpStepKind.choice:
        final choice = choiceOf(target.choiceKey!);
        return choice == null ? null : validateChoice(choice);
      case LevelUpStepKind.review:
        for (var i = 0; i < all.length - 1; i++) {
          final error = validate(i);
          if (error != null) return error;
        }
        return null;
    }
  }

  /// Every page is complete and nothing is being sent.
  bool get canConfirm => !submitting && validate(steps.length - 1) == null;

  /// Body of the POST with the current answers. Optional choices left empty
  /// are not sent.
  LevelUpRequest get request {
    final answers = <LevelUpChoiceAnswer>[];
    for (final choice in visibleChoices) {
      final selection = selectionOf(choice.key);
      if (choice.kind == LevelChoiceKind.asiOrFeat) {
        if (choice.required == 0 && validateChoice(choice) != null) continue;
        if (!_featMode(choice, selection)) {
          answers.add(
            LevelUpChoiceAnswer.asi(choice.key, {
              for (final e in selection.asi.entries)
                if (e.value > 0) e.key: e.value,
            }),
          );
        } else if (selection.feat != null) {
          final feat = choice.option(selection.feat!);
          final increase = feat?.abilityIncrease;
          answers.add(
            LevelUpChoiceAnswer.feat(
              choice.key,
              selection.feat!,
              ability: increase == null
                  ? null
                  : selection.featAbility ?? (increase.needsPick ? null : increase.options.first),
            ),
          );
        }
        continue;
      }
      final picks = [
        for (final s in selection.selected)
          if (s.trim().isNotEmpty) choice.freeText ? s.trim() : s,
      ];
      if (picks.isEmpty && selection.replaced == null && choice.required == 0) continue;
      answers.add(LevelUpChoiceAnswer.picks(choice.key, picks, replaced: [?selection.replaced]));
    }
    return LevelUpRequest(
      classIndex: plan?.classIndex,
      hitPointsRolled: hitPointsRolled ?? 0,
      choices: answers,
    );
  }
}

/// Spanish message of a failed level-up request: the server's explanation
/// when it sent one (400 and 409 carry it), else a generic text.
String describeLevelUpError(Object error) {
  final detail = problemDetail(error);
  if (detail != null) return detail;
  if (error is DioException) {
    final data = error.response?.data;
    if (data is Map && data['errors'] is Map) {
      for (final messages in (data['errors'] as Map).values) {
        if (messages is List && messages.isNotEmpty) return '${messages.first}';
      }
    }
  }
  return describeCharacterError(
    error,
    byStatus: const {409: 'No tienes ningún nivel concedido por el DM.'},
  );
}

/// The level-up wizard of the character with id [characterId]: loads the plan
/// of the chosen class (again when the class changes), keeps the answers by
/// choice and applies the level.
class LevelUpController extends Notifier<LevelUpState> {
  LevelUpController(this.characterId);

  final String characterId;

  /// Bumped on each plan request so a late answer for another class is dropped.
  int _request = 0;

  CharactersRepository get _repository => ref.read(charactersRepositoryProvider);

  Dnd5eCharactersRepository get _dnd5e => ref.read(dnd5eCharactersRepositoryProvider);

  @override
  LevelUpState build() {
    Future.microtask(() => _loadPlan(null));
    Future.microtask(_loadCharacter);
    return const LevelUpState();
  }

  Future<void> _loadCharacter() async {
    try {
      final character = await _repository.get(characterId);
      if (ref.mounted) state = state.copyWith(character: character);
    } catch (_) {
      // Only the ASI caps use it; the server checks them again.
    }
  }

  Future<void> _loadPlan(String? classIndex) async {
    final request = ++_request;
    state = state.copyWith(loading: true, loadError: null);
    try {
      final plan = await _dnd5e.levelUpPlan(characterId, classIndex: classIndex);
      if (!ref.mounted || request != _request) return;
      state = state.copyWith(
        plan: plan,
        classIndex: plan.classIndex,
        loading: false,
        selections: const {},
        submitError: null,
      );
    } catch (error) {
      if (!ref.mounted || request != _request) return;
      state = state.copyWith(loading: false, loadError: describeLevelUpError(error));
    }
  }

  /// Retries the plan of the selected class.
  Future<void> reload() => _loadPlan(state.classIndex);

  /// Gains the level in [classIndex] instead: loads its plan; the answers of
  /// the previous class are discarded.
  Future<void> selectClass(String classIndex) async {
    if (classIndex == state.selectedClassIndex && state.planIsCurrent) return;
    state = state.copyWith(classIndex: classIndex, selections: const {});
    await _loadPlan(classIndex);
  }

  // -- Navigation ----------------------------------------------------------------

  void goTo(int step) {
    final last = state.steps.length - 1;
    state = state.copyWith(step: step.clamp(0, last), submitError: null);
  }

  void back() => goTo(state.step - 1);

  /// Moves on when the current page is valid; returns its error otherwise.
  String? next() {
    final error = state.validate(state.step);
    if (error != null) return error;
    goTo(state.step + 1);
    return null;
  }

  String? validate(int step) => state.validate(step);

  bool get canConfirm => state.canConfirm;

  // -- Answers -------------------------------------------------------------------

  void setHitPoints(String text) => state = state.copyWith(hitPointsText: text);

  void _update(String key, LevelUpSelection Function(LevelUpSelection) change) {
    state = state.copyWith(
      selections: {...state.selections, key: change(state.selectionOf(key))},
      submitError: null,
    );
  }

  /// Picks or unpicks [index] in [choice]. Beyond the picks needed, a single
  /// pick is replaced and further picks are ignored.
  void toggleOption(LevelUpChoice choice, String index) {
    final option = choice.option(index);
    _update(choice.key, (s) {
      final selected = [...s.selected];
      if (selected.remove(index)) return s.copyWith(selected: selected);
      if (option != null && (!option.eligible || !state.isAvailable(option))) return s;
      final needed = state.neededPicks(choice);
      if (selected.length < needed) return s.copyWith(selected: [...selected, index]);
      if (needed == 1) return s.copyWith(selected: [index]);
      return s;
    });
    // A new subclass changes which choices apply: drop the answers of the others.
    if (choice.kind == LevelChoiceKind.subclass) _dropHiddenSelections();
    _dropUnavailablePicks();
  }

  /// Unpicks options whose required pick of this level-up was undone (an
  /// expertise in a skill that is no longer picked).
  void _dropUnavailablePicks() {
    final plan = state.plan;
    if (plan == null) return;
    var selections = state.selections;
    for (final choice in plan.choices) {
      final selection = selections[choice.key];
      if (selection == null || !choice.options.any((o) => o.requires != null)) continue;
      final kept = [
        for (final index in selection.selected)
          if (choice.option(index) == null || state.isAvailable(choice.option(index)!)) index,
      ];
      if (kept.length != selection.selected.length) {
        selections = {...selections, choice.key: selection.copyWith(selected: kept)};
        state = state.copyWith(selections: selections);
      }
    }
  }

  void _dropHiddenSelections() {
    final visible = {for (final c in state.visibleChoices) c.key};
    state = state.copyWith(
      selections: {
        for (final e in state.selections.entries)
          if (visible.contains(e.key)) e.key: e.value,
      },
    );
  }

  /// Writes the [slot]-th value of a free-text [choice].
  void setText(LevelUpChoice choice, int slot, String text) {
    _update(choice.key, (s) {
      final values = [...s.selected];
      while (values.length <= slot) {
        values.add('');
      }
      values[slot] = text;
      return s.copyWith(selected: values);
    });
  }

  /// Swaps out the known pick [index] (null: no replacement).
  void setReplaced(LevelUpChoice choice, String? index) {
    _update(choice.key, (s) {
      final next = s.copyWith(replaced: index);
      final needed = choice.required + (index == null ? 0 : 1);
      if (choice.freeText || next.selected.length <= needed) return next;
      return next.copyWith(selected: next.selected.sublist(0, needed));
    });
  }

  void setImprovementMode(LevelUpChoice choice, ImprovementMode mode) =>
      _update(choice.key, (s) => s.copyWith(mode: mode));

  /// Adds [delta] points of the improvement to [ability], within 0..2 per
  /// ability, 2 in total and the cap of 20.
  void adjustAsi(LevelUpChoice choice, String ability, int delta) {
    _update(choice.key, (s) {
      final current = s.asi[ability] ?? 0;
      final next = current + delta;
      if (next < 0 || next > improvementPoints) return s;
      if (delta > 0) {
        if (s.asiTotal + delta > improvementPoints) return s;
        if (state.naturalScore(ability) + next > improvementMaxScore) return s;
      }
      return s.copyWith(asi: {...s.asi, ability: next}, mode: ImprovementMode.asi);
    });
  }

  void selectFeat(LevelUpChoice choice, String index) {
    final option = choice.option(index);
    if (option == null || !option.eligible) return;
    _update(
      choice.key,
      (s) => s.copyWith(
        feat: s.feat == index ? null : index,
        featAbility: null,
        mode: ImprovementMode.feat,
      ),
    );
    _dropUnavailablePicks();
  }

  void setFeatAbility(LevelUpChoice choice, String ability) =>
      _update(choice.key, (s) => s.copyWith(featAbility: ability));

  // -- Submission ------------------------------------------------------------------

  /// Applies the level. True on success ([LevelUpState.completed] holds the
  /// updated character); false with [LevelUpState.submitError] otherwise.
  Future<bool> submit() async {
    if (!state.canConfirm) return false;
    state = state.copyWith(submitting: true, submitError: null);
    try {
      final detail = await _dnd5e.applyLevelUp(characterId, state.request);
      if (!ref.mounted) return true;
      state = state.copyWith(submitting: false, completed: detail);
      ref.invalidate(characterControllerProvider(characterId));
      ref.invalidate(campaignCharactersControllerProvider(detail.campaignId));
      return true;
    } catch (error) {
      if (!ref.mounted) return false;
      state = state.copyWith(submitting: false, submitError: describeLevelUpError(error));
      return false;
    }
  }
}

final levelUpControllerProvider = NotifierProvider.autoDispose
    .family<LevelUpController, LevelUpState, String>(LevelUpController.new);
