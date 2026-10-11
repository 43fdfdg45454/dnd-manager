import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/auth/auth_controller.dart';
import 'package:opentrpg_core/core/auth/auth_state.dart';
import 'package:opentrpg_core/features/campaigns/data/campaigns_controller.dart';

import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import '../../domain/class_theme.dart';
import '../../models.dart';
import 'character_wizard_page.dart' show WizardStepInfo;
import 'step_basics.dart' show stepPadding;
import 'step_proficiencies.dart' show languageLabel;

/// Summary by sections with an "Editar" button that jumps to each step.
class ReviewStep extends ConsumerStatefulWidget {
  const ReviewStep({super.key, required this.args});

  final WizardArgs args;

  @override
  ConsumerState<ReviewStep> createState() => _ReviewStepState();
}

class _ReviewStepState extends ConsumerState<ReviewStep> {
  late final _notes = TextEditingController(
    text: ref.read(characterWizardControllerProvider(widget.args)).notes,
  );

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  /// "PNJ" or the player's name, never "Yo": a player creates their own
  /// character and a DM has none (without a player chosen it is an NPC).
  String _ownerText(WizardState state) {
    final campaign = ref.watch(campaignDetailControllerProvider(widget.args.campaignId)).value;
    final auth = ref.watch(authControllerProvider);
    final me = auth is AuthSignedIn ? auth.user : null;
    if (!(campaign?.myRole.isAtLeastDm ?? false)) return me?.displayName ?? 'Jugador';
    final ownerId = state.owner?.userId;
    if (ownerId == null) return 'PNJ';
    return campaign?.members.where((m) => m.userId == ownerId).firstOrNull?.displayName ??
        'Otro jugador';
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(characterWizardControllerProvider(widget.args));
    final controller = ref.read(characterWizardControllerProvider(widget.args).notifier);
    final steps = state.steps;
    final errors = {for (final e in state.invalidSteps) e.step: e.error};
    final scheme = Theme.of(context).colorScheme;

    // A step that does not validate (also after a later change, such as
    // another class emptying the skills) is marked in the error colour with
    // its error; "Editar" goes straight to it.
    Widget section(WizardStep step, List<String> lines) {
      final error = errors[step];
      return Card(
        key: Key('review-${step.name}'),
        shape: error == null
            ? null
            : RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
                side: BorderSide(color: scheme.error, width: 2),
              ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      step.titleIn(state),
                      style: Theme.of(context).textTheme.titleSmall
                          ?.copyWith(color: error == null ? null : scheme.error),
                    ),
                    for (final line in lines) Text(line),
                    if (error != null)
                      Row(
                        key: Key('review-error-${step.name}'),
                        children: [
                          Icon(Icons.error_outline, size: 16, color: scheme.error),
                          const SizedBox(width: 4),
                          Expanded(
                            child: Text(error, style: TextStyle(color: scheme.error)),
                          ),
                        ],
                      ),
                  ],
                ),
              ),
              TextButton(
                key: Key('review-edit-${step.name}'),
                style: error == null ? null : TextButton.styleFrom(foregroundColor: scheme.error),
                onPressed: () => controller.goTo(steps.indexOf(step)),
                child: const Text('Editar'),
              ),
            ],
          ),
        ),
      );
    }

    final race = state.race;
    final subrace = state.subrace;
    final detail = state.classDetail;
    final scores = [
      for (final k in abilityKeys) '${abilityAbbreviation(k)} ${state.finalScore(k) ?? '—'}',
    ].join(' · ');

    return ListView(
      key: const Key('step-review'),
      padding: stepPadding,
      children: [
        section(WizardStep.name, [
          state.name.trim(),
          if (state.alignment != null) alignmentLabel(state.alignment!),
          'Para: ${_ownerText(state)}',
        ]),
        section(WizardStep.race, [
          [race?.name ?? state.raceIndex ?? '—', if (subrace != null) subrace.name].join(' · '),
          if (!state.applyRacialBonuses) 'Sin bonos raciales',
        ]),
        section(WizardStep.classChoice, [
          [
            classThemeOf(state.classIndex).labelEs == adventurerTheme.labelEs
                ? (detail?.name ?? state.classIndex ?? '—')
                : classThemeOf(state.classIndex).labelEs,
            'Nivel 1',
            if (state.subclassIndex != null)
              detail?.subclasses.where((s) => s.index == state.subclassIndex).firstOrNull?.name ??
                  state.subclassIndex!,
          ].join(' · '),
        ]),
        section(WizardStep.abilities, [state.method.label, scores]),
        section(WizardStep.background, [
          'Trasfondo: ${state.background?.name ?? 'ninguno'}',
          if (state.skills.isNotEmpty || state.backgroundSkills.isNotEmpty)
            'Habilidades: ${[...state.skills, ...state.backgroundSkills].map(skillLabel).join(', ')}',
          if (state.allLanguages.isNotEmpty)
            'Idiomas: ${state.allLanguages.map(languageLabel).join(', ')}',
        ]),
        section(WizardStep.personality, [
          for (final kind in PersonalityKind.values)
            if (state.personalityText(kind) != null)
              '${kind.label}: ${state.personalityText(kind)!.replaceAll('\n', ' · ')}',
          if (state.backgroundDetail.trim().isNotEmpty) state.backgroundDetail.trim(),
          if (state.personalityWarning != null) 'Incompleta (puedes completarla en la hoja)',
        ]),
        if (steps.contains(WizardStep.starterFeat))
          section(WizardStep.starterFeat, [
            if (state.starterFeatChoice case final choice?)
              if (state.originAnswerOf(choice).feat case final feat?)
                choice.option(feat)?.name ?? feat
              else
                'Sin dote inicial',
          ]),
        section(WizardStep.equipment, [
          if (state.allEquipment.isEmpty &&
              state.startingCopper == 0 &&
              state.trinket == null &&
              state.trinketDescription == null)
            'Sin equipo inicial',
          for (final e in state.allEquipment) '${e.qty} × ${e.name}',
          if (state.trinket != null) 'Baratija: ${state.trinket!.name}',
          if (state.trinketDescription != null) 'Baratija: ${state.trinketDescription}',
          if (state.startingCopper > 0) 'Oro inicial: ${copperToGoldText(state.startingCopper)} po',
        ]),
        if (steps.contains(WizardStep.spells))
          section(WizardStep.spells, [
            if (state.spells.isEmpty) 'Sin hechizos elegidos',
            for (final s in state.spells)
              if (state.hasSpellbook &&
                  s.level != 0 &&
                  !state.preparedChosen.contains(s.spellIndex))
                '${s.name ?? s.spellIndex} (en el libro)'
              else
                s.name ?? s.spellIndex,
          ]),
        if (steps.contains(WizardStep.origin))
          section(WizardStep.origin, [
            for (final c in state.originChoices)
              if (!state.originAnswerOf(c).isEmpty)
                '${c.name}: ${[for (final p in state.originAnswerOf(c).picks)
                  if (p.trim().isNotEmpty) c.option(p)?.name ?? p, if (state.originAnswerOf(c).feat != null) c.option(state.originAnswerOf(c).feat!)?.name ?? state.originAnswerOf(c).feat!].join(', ')}',
          ]),
        const SizedBox(height: 8),
        TextField(
          key: const Key('wizard-notes'),
          controller: _notes,
          minLines: 2,
          maxLines: 5,
          decoration: const InputDecoration(labelText: 'Notas (opcional)'),
          onChanged: controller.setNotes,
        ),
      ],
    );
  }
}
