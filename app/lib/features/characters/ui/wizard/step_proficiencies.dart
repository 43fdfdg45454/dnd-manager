import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/components.dart';
import '../../../../core/ui/selection_grid.dart';
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

/// Standard languages of the SRD (the rest are exotic).
const _standardLanguages = {
  'Common',
  'Dwarvish',
  'Elvish',
  'Giant',
  'Gnomish',
  'Goblin',
  'Halfling',
  'Orc',
};

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
            SectionHeader(
              'Habilidades de clase',
              padding: const EdgeInsets.only(top: 20, bottom: 4),
            ),
            Text(
              'Habilidades de clase ${state.skills.length}/${choices.choose}',
              key: const Key('wizard-skills-counter'),
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 8),
            SelectionGrid(
              onToggle: controller.toggleSkill,
              items: [
                for (final skill in choices.from)
                  SelectionItem(
                    id: skill,
                    tileKey: Key('skill-$skill'),
                    label: skillLabel(skill),
                    caption: granted.contains(skill)
                        ? 'Del trasfondo'
                        : abilityAbbreviation(skillAbilities[skill] ?? ''),
                    state: granted.contains(skill)
                        ? SelectionState.locked
                        : state.skills.contains(skill)
                        ? SelectionState.selected
                        : state.skills.length >= choices.choose
                        ? SelectionState.blocked
                        : SelectionState.available,
                  ),
              ],
            ),
          ],
          SectionHeader('Idiomas', padding: const EdgeInsets.only(top: 20, bottom: 4)),
          Text(
            state.languages.isEmpty
                ? 'Marca los idiomas que habla tu personaje.'
                : '${state.languages.length} seleccionados',
            style: theme.textTheme.bodySmall,
          ),
          for (final group in [
            ('Estándar', languages.where(_standardLanguages.contains).toList()),
            ('Exóticos', languages.where((l) => !_standardLanguages.contains(l)).toList()),
          ])
            if (group.$2.isNotEmpty) ...[
              Padding(
                padding: const EdgeInsets.only(top: 12, bottom: 6),
                child: Text(group.$1, style: theme.textTheme.labelLarge),
              ),
              SelectionGrid(
                onToggle: controller.toggleLanguage,
                items: [
                  for (final language in group.$2)
                    SelectionItem(
                      id: language,
                      tileKey: Key('lang-$language'),
                      label: languageLabel(language),
                      state: state.languages.contains(language)
                          ? SelectionState.selected
                          : SelectionState.available,
                    ),
                ],
              ),
            ],
        ],
      ),
    );
  }
}
