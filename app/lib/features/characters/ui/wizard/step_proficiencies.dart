import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/domain/catalog_format.dart';
import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import 'step_basics.dart' show WizardLoadError, stepPadding;

const _noBackground = '__none__';

/// SRD languages with their Spanish name.
const srdLanguages = <String, String>{
  'Common': 'Común',
  'Dwarvish': 'Enano',
  'Elvish': 'Élfico',
  'Giant': 'Gigante',
  'Gnomish': 'Gnomo',
  'Goblin': 'Goblin',
  'Halfling': 'Mediano',
  'Orc': 'Orco',
  'Abyssal': 'Abisal',
  'Celestial': 'Celestial',
  'Draconic': 'Dracónico',
  'Deep Speech': 'Habla profunda',
  'Infernal': 'Infernal',
  'Primordial': 'Primordial',
  'Sylvan': 'Silvano',
  'Undercommon': 'Infracomún',
};

String languageLabel(String language) => srdLanguages[language] ?? language;

// ---------------------------------------------------------------------------
// 5. Background and proficiencies
// ---------------------------------------------------------------------------

/// Background, class skill choices and languages.
class BackgroundStep extends ConsumerWidget {
  const BackgroundStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final backgrounds = ref.watch(backgroundsProvider);
    final theme = Theme.of(context);
    final choices = state.classDetail?.skillChoices;
    final granted = state.backgroundSkills;
    final languages = <String>{...srdLanguages.keys, ...state.languages};

    return backgrounds.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) =>
          WizardLoadError(error: error, onRetry: () => ref.invalidate(backgroundsProvider)),
      data: (list) => ListView(
        key: const Key('step-background'),
        padding: stepPadding,
        children: [
          DropdownButtonFormField<String>(
            key: const Key('wizard-background'),
            initialValue: state.backgroundIndex ?? _noBackground,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Trasfondo'),
            items: [
              const DropdownMenuItem(value: _noBackground, child: Text('Sin trasfondo')),
              for (final b in list) DropdownMenuItem(value: b.index, child: Text(b.name)),
            ],
            onChanged: (value) =>
                controller.selectBackground(list.where((b) => b.index == value).firstOrNull),
          ),
          if (state.background != null) ...[
            const SizedBox(height: 8),
            if (granted.isNotEmpty)
              Text(
                'Habilidades del trasfondo: ${granted.map(skillLabel).join(', ')}',
                key: const Key('wizard-background-skills'),
              ),
            if (state.background!.startingEquipmentText != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Equipo: ${cleanText(state.background!.startingEquipmentText!)}',
                  style: theme.textTheme.bodySmall,
                ),
              ),
          ],
          if (choices != null && choices.choose > 0) ...[
            const SizedBox(height: 16),
            Text(
              'Habilidades de clase ${state.skills.length}/${choices.choose}',
              key: const Key('wizard-skills-counter'),
              style: theme.textTheme.titleSmall,
            ),
            const SizedBox(height: 4),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final skill in choices.from)
                  FilterChip(
                    key: Key('skill-$skill'),
                    label: Text(skillLabel(skill)),
                    selected: state.skills.contains(skill) || granted.contains(skill),
                    onSelected: granted.contains(skill)
                        ? null
                        : (_) => controller.toggleSkill(skill),
                  ),
              ],
            ),
          ],
          const SizedBox(height: 16),
          Text('Idiomas', style: theme.textTheme.titleSmall),
          const SizedBox(height: 4),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final language in languages)
                FilterChip(
                  key: Key('lang-$language'),
                  label: Text(languageLabel(language)),
                  selected: state.languages.contains(language),
                  onSelected: (_) => controller.toggleLanguage(language),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
