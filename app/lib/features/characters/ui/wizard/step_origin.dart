import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/character_wizard_controller.dart';
import '../level_up/level_up_widgets.dart' show LevelUpHeading;
import '../origin_choices_widgets.dart';
import 'step_basics.dart' show stepPadding;

/// "Elecciones de raza y trasfondo": the decisions the race, subrace and
/// background ask for (ability bonuses, skills, tools, a cantrip, a feat or an
/// ancestry), with the same option cards as the level-up. The required ones
/// block "Siguiente"; optional ones are shown but never block. Languages are
/// asked in the background step.
class OriginStep extends ConsumerWidget {
  const OriginStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);

    if (state.originError != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.originError!, key: const Key('origin-error'), textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton.icon(
                key: const Key('origin-retry'),
                onPressed: controller.loadOrigin,
                icon: const Icon(Icons.refresh),
                label: const Text('Reintentar'),
              ),
            ],
          ),
        ),
      );
    }
    if (state.originLoading || state.originPlan == null) {
      return const Center(child: CircularProgressIndicator(key: Key('origin-loading')));
    }
    final choices = state.originChoices;
    return ListView(
      key: const Key('step-origin'),
      padding: stepPadding,
      children: [
        const LevelUpHeading(
          'Tu origen',
          subtitle: 'Tu raza y tu trasfondo te piden decidir lo siguiente.',
        ),
        if (choices.isEmpty) const Text('No hay nada que elegir.'),
        for (final choice in choices)
          OriginChoiceView(
            choice: choice,
            answer: state.originAnswerOf(choice),
            onToggle: (index) => controller.toggleOriginOption(choice, index),
            onText: (slot, text) => controller.setOriginText(choice, slot, text),
            onFeat: (index) => controller.selectOriginFeat(choice, index),
            onFeatAbility: (ability) => controller.setOriginFeatAbility(choice, ability),
          ),
      ],
    );
  }
}
