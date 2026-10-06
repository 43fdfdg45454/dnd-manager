import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../campaigns/data/campaigns_controller.dart';
import '../../data/character_wizard_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import '../../domain/class_theme.dart';
import 'character_wizard_page.dart' show WizardStepInfo;
import 'step_proficiencies.dart' show languageLabel;
import 'step_basics.dart' show stepPadding;

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

  String _ownerText(WizardState state) {
    final owner = state.owner;
    if (owner == null) return 'Yo';
    if (owner.userId == null) return 'Sin dueño (PNJ)';
    final members = ref
        .read(campaignDetailControllerProvider(widget.args.campaignId))
        .value
        ?.members;
    return members?.where((m) => m.userId == owner.userId).firstOrNull?.displayName ??
        'Otro jugador';
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(characterWizardControllerProvider(widget.args));
    final controller = ref.read(characterWizardControllerProvider(widget.args).notifier);
    final steps = state.steps;

    Widget section(WizardStep step, List<String> lines) => Card(
      key: Key('review-${step.name}'),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(step.title, style: Theme.of(context).textTheme.titleSmall),
                  for (final line in lines) Text(line),
                ],
              ),
            ),
            TextButton(
              key: Key('review-edit-${step.name}'),
              onPressed: () => controller.goTo(steps.indexOf(step)),
              child: const Text('Editar'),
            ),
          ],
        ),
      ),
    );

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
          'Dueño: ${_ownerText(state)}',
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
          if (state.languages.isNotEmpty)
            'Idiomas: ${state.languages.map(languageLabel).join(', ')}',
        ]),
        section(WizardStep.equipment, [
          if (state.equipment.isEmpty) 'Sin equipo inicial',
          for (final e in state.equipment) '${e.qty} × ${e.name}',
        ]),
        if (steps.contains(WizardStep.spells))
          section(WizardStep.spells, [
            if (state.spells.isEmpty) 'Sin hechizos elegidos',
            for (final s in state.spells) s.name ?? s.spellIndex,
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
