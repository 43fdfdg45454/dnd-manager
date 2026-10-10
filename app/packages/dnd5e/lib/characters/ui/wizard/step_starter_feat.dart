import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/character_wizard_controller.dart';
import '../level_up/level_up_widgets.dart' show LevelUpHeading;
import '../origin_choices_widgets.dart';
import 'step_basics.dart' show stepPadding;

/// "Dote inicial": the optional feat the campaign offers at creation (its
/// rules enable starter feats and an enabled content pack brings them). It
/// never blocks "Siguiente".
class StarterFeatStep extends ConsumerWidget {
  const StarterFeatStep({super.key, required this.args});

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
              Text(
                state.originError!,
                key: const Key('starter-feat-error'),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                key: const Key('starter-feat-retry'),
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
      return const Center(child: CircularProgressIndicator(key: Key('starter-feat-loading')));
    }
    final choice = state.starterFeatChoice;
    return ListView(
      key: const Key('step-starter-feat'),
      padding: stepPadding,
      children: [
        const LevelUpHeading(
          'Dote inicial',
          subtitle:
              'La campaña permite empezar con una dote inicial. Es opcional: '
              'puedes seguir sin elegir ninguna.',
        ),
        if (choice == null)
          const Text(
            'No hay dotes iniciales disponibles para este personaje.',
            key: Key('starter-feat-none'),
          )
        else
          OriginChoiceView(
            choice: choice,
            answer: state.originAnswerOf(choice),
            onToggle: (index) => controller.toggleOriginOption(choice, index),
            onText: (slot, text) => controller.setOriginText(choice, slot, text),
            onFeat: (index) => controller.selectOriginFeat(choice, index),
            onFeatAbility: (ability) => controller.setOriginFeatAbility(choice, ability),
            onRemovedSave: (ability) => controller.toggleOriginRemovedSave(choice, ability),
          ),
      ],
    );
  }
}
