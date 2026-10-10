import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/textures.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';

import '../../data/level_up_controller.dart';
import '../../domain/character_format.dart' show abilityAbbreviation;
import '../../domain/class_theme.dart';
import '../../models.dart';
import 'hit_points_step.dart' show hitPointsPreview;
import 'level_up_widgets.dart';

/// What was picked for [choice], in Spanish ("Defensa", "+1 Fue, +1 Des",
/// "Grappler (+1 Fue)"), or null when nothing was.
String? describeSelection(LevelUpState state, LevelUpChoice choice) {
  final selection = state.selectionOf(choice.key);
  if (choice.kind == LevelChoiceKind.asiOrFeat) {
    if (selection.mode == ImprovementMode.asi) {
      final parts = [
        for (final ability in abilityKeys)
          if ((selection.asi[ability] ?? 0) > 0)
            '+${selection.asi[ability]} ${abilityAbbreviation(ability)}',
      ];
      return parts.isEmpty ? null : parts.join(', ');
    }
    final feat = selection.feat == null ? null : choice.option(selection.feat!);
    if (feat == null) return null;
    final increase = feat.abilityIncrease;
    final ability =
        selection.featAbility ??
        (increase != null && !increase.needsPick ? increase.options.first : null);
    return increase == null || ability == null
        ? feat.name
        : '${feat.name} (+${increase.amount} ${abilityAbbreviation(ability)})';
  }
  final names = [
    for (final s in selection.selected)
      if (s.trim().isNotEmpty) choice.freeText ? s.trim() : (choice.option(s)?.name ?? s),
  ];
  final replaced = selection.replaced == null
      ? null
      : choice.known.where((k) => k.index == selection.replaced).map((k) => k.name).firstOrNull ??
            selection.replaced;
  if (names.isEmpty && replaced == null) return null;
  return [
    if (names.isNotEmpty) names.join(', '),
    if (replaced != null) 'sustituye a $replaced',
  ].join(' · ');
}

/// Last page: a summary per page with "Editar" to go back to it, the features
/// gained at the new level, a note for classes that prepare spells and any
/// error of the submission. "Confirmar" lives in the bottom bar.
class LevelUpReviewStep extends ConsumerWidget {
  const LevelUpReviewStep({super.key, required this.characterId});

  final String characterId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(levelUpControllerProvider(characterId));
    final controller = ref.read(levelUpControllerProvider(characterId).notifier);
    final plan = state.plan;
    if (plan == null) return const SizedBox.shrink();
    final steps = state.steps;
    final theme = Theme.of(context);
    final selectedClass = plan.selectedClass;
    final className = classThemes[plan.classIndex]?.labelEs ?? selectedClass?.name ?? '';

    Widget row(LevelUpStep step, String title, String? value, {String? error}) {
      final index = steps.indexOf(step);
      return _ReviewRow(
        key: Key('levelup-review-${step.id}'),
        title: title,
        value: value ?? 'Sin elegir',
        error: error,
        onEdit: () => controller.goTo(index),
        editKey: Key('levelup-edit-${step.id}'),
      );
    }

    return LevelUpStepList(
      children: [
        LevelUpHeading(
          'Resumen',
          subtitle: 'Revisa tus elecciones antes de subir a nivel ${plan.targetLevel}.',
        ),
        row(
          LevelUpStep.classChoice,
          'Clase',
          '$className ${plan.classLevel}',
          error: state.validate(steps.indexOf(LevelUpStep.classChoice)),
        ),
        row(
          LevelUpStep.hitPoints,
          'Puntos de golpe',
          state.hitPointsRolled == null
              ? null
              : '1d${plan.hitDie}: ${state.hitPointsRolled} '
                    '${hitPointsPreview(rolled: state.hitPointsRolled, conModifier: plan.conModifier)}',
          error: state.validate(steps.indexOf(LevelUpStep.hitPoints)),
        ),
        for (final choice in state.visibleChoices)
          row(
            LevelUpStep.choice(choice.key),
            choice.name,
            describeSelection(state, choice) ?? (choice.required == 0 ? 'Nada (opcional)' : null),
            error: state.validateChoice(choice),
          ),
        _NewFeatures(features: plan.newFeaturesFor(state.subclassIndex)),
        if (plan.spellcasting?.preparesSpells ?? false)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: Row(
              key: const Key('levelup-prepare-note'),
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.auto_stories, size: 18, color: context.tokens.arcane),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Al confirmar se abre "Preparar conjuros" para revisar los conjuros '
                    'preparados con tu nuevo nivel.',
                    style: theme.textTheme.bodySmall,
                  ),
                ),
              ],
            ),
          ),
        if (state.submitError != null)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: Text(
              state.submitError!,
              key: const Key('levelup-submit-error'),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
      ],
    );
  }
}

/// "Rasgos nuevos": the class and subclass features of the new level, each
/// with its description collapsed.
class _NewFeatures extends StatelessWidget {
  const _NewFeatures({required this.features});

  final List<LevelUpNewFeature> features;

  @override
  Widget build(BuildContext context) {
    if (features.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Column(
      key: const Key('levelup-new-features'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 16, bottom: 4),
          child: Text('Rasgos nuevos', style: theme.textTheme.titleMedium),
        ),
        for (final feature in features)
          RuneCard(
            key: Key('levelup-new-feature-${feature.name}'),
            margin: const EdgeInsets.symmetric(vertical: 4),
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(feature.name, style: theme.textTheme.labelLarge),
                if (feature.description.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  ExpandableText(feature.description),
                ],
              ],
            ),
          ),
      ],
    );
  }
}

class _ReviewRow extends StatelessWidget {
  const _ReviewRow({
    super.key,
    required this.title,
    required this.value,
    required this.onEdit,
    required this.editKey,
    this.error,
  });

  final String title;
  final String value;
  final String? error;
  final VoidCallback onEdit;
  final Key editKey;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    return RuneCard(
      margin: const EdgeInsets.symmetric(vertical: 4),
      padding: const EdgeInsets.fromLTRB(12, 8, 4, 8),
      borderColor: error == null ? null : tokens.blood,
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.labelLarge),
                Text(value, style: theme.textTheme.bodyMedium),
                if (error != null)
                  Text(error!, style: theme.textTheme.bodySmall?.copyWith(color: tokens.blood)),
              ],
            ),
          ),
          TextButton(key: editKey, onPressed: onEdit, child: const Text('Editar')),
        ],
      ),
    );
  }
}
