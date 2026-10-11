import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/theme/components.dart';
import 'package:opentrpg_core/core/ui/selection_grid.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/domain/catalog_format.dart';
import '../../../catalog/data/models.dart' show ReferenceEntry, ReferenceKinds, Skill;
import '../../../catalog/ui/catalog_detail_links.dart';
import '../../../catalog/ui/condition_sheet.dart' show showInfoSheet;
import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import '../level_up/level_up_widgets.dart' show ExpandableText;
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
    final backgrounds = ref.watch(backgroundsProvider(args.campaignId));
    final skillCatalog = ref.watch(skillsProvider).value ?? const <String, Skill>{};
    final languageCatalog = _languagesByName(
      ref.watch(referenceProvider(ReferenceKinds.languages)).value,
    );
    final theme = Theme.of(context);
    final choices = state.classDetail?.skillChoices;
    final skillsToChoose = state.classSkillsToChoose;
    final granted = state.backgroundSkills;
    final fixed = state.fixedLanguages;
    final toChoose = state.languagesToChoose;
    final remaining = state.languagesRemaining;
    // Once the user tried to leave the step, the counter that does not match
    // turns red (the message is at the bottom of the wizard).
    final showErrors = state.showsErrorsOf(WizardStep.background);
    final skillsInvalid = showErrors && skillsToChoose > 0 && state.skills.length != skillsToChoose;
    final languagesInvalid =
        showErrors &&
        (state.languages.length > toChoose ||
            state.distributeLanguages(state.languages.toList()) == null);
    TextStyle? counterStyle(bool invalid) => invalid
        ? theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)
        : theme.textTheme.bodySmall;
    final languages = <String>{
      ...srdLanguages.keys,
      ...fixed,
      ...state.languages,
      for (final slot in state.languageSlots) ...slot.from,
    };

    return backgrounds.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => WizardLoadError(
        error: error,
        onRetry: () => ref.invalidate(backgroundsProvider(args.campaignId)),
      ),
      data: (list) => ListView(
        key: const Key('step-background'),
        padding: stepPadding,
        children: [
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<String>(
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
              ),
              const SizedBox(width: 4),
              DetailInfoButton(
                key: Key('detail-background-${state.background?.index ?? 'none'}'),
                onPressed: state.background == null
                    ? null
                    : () => openBackgroundDetail(context, state.background!),
              ),
            ],
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
            if ((state.background!.featureName ?? '').isNotEmpty ||
                state.background!.featureDescription.isNotEmpty)
              Padding(
                key: const Key('wizard-background-feature'),
                padding: const EdgeInsets.only(top: 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if ((state.background!.featureName ?? '').isNotEmpty)
                      Text(state.background!.featureName!, style: theme.textTheme.titleSmall),
                    ExpandableText(state.background!.featureDescription),
                  ],
                ),
              ),
          ],
          if (choices != null && choices.choose > 0) ...[
            SectionHeader(
              'Habilidades de clase',
              padding: const EdgeInsets.only(top: 20, bottom: 4),
            ),
            Text(
              'Habilidades de clase ${state.skills.length}/$skillsToChoose',
              key: const Key('wizard-skills-counter'),
              style: counterStyle(skillsInvalid),
            ),
            if (state.starterSkillChoicesDelta != 0)
              Text(
                state.starterSkillChoicesDelta < 0
                    ? 'Tu dote inicial resta ${-state.starterSkillChoicesDelta} a las habilidades de clase'
                    : 'Tu dote inicial suma ${state.starterSkillChoicesDelta} a las habilidades de clase',
                key: const Key('wizard-skills-delta'),
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
                    infoKey: Key('skill-info-$skill'),
                    onInfo: skillCatalog[skill] == null
                        ? null
                        : () => _showSkill(context, skill, skillCatalog[skill]!),
                    label: skillLabel(skill),
                    caption: granted.contains(skill)
                        ? 'Del trasfondo'
                        : abilityAbbreviation(skillAbilities[skill] ?? ''),
                    state: granted.contains(skill)
                        ? SelectionState.locked
                        : state.skills.contains(skill)
                        ? SelectionState.selected
                        : state.skills.length >= skillsToChoose
                        ? SelectionState.blocked
                        : SelectionState.available,
                  ),
              ],
            ),
          ],
          SectionHeader('Idiomas', padding: const EdgeInsets.only(top: 20, bottom: 4)),
          Text(
            toChoose == 0
                ? 'Tu raza y trasfondo no te dan idiomas adicionales'
                : 'Idiomas a elegir ${state.languages.length}/$toChoose',
            key: const Key('wizard-languages-counter'),
            style: counterStyle(languagesInvalid),
          ),
          if (toChoose > 0 && remaining > 0)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                remaining == 1
                    ? 'Te queda 1 idioma por elegir'
                    : 'Te quedan $remaining idiomas por elegir',
                key: const Key('wizard-languages-remaining'),
                style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.tertiary),
              ),
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
                      infoKey: Key('lang-info-$language'),
                      onInfo: languageCatalog[language.toLowerCase()] == null
                          ? null
                          : () => _showLanguage(
                              context,
                              language,
                              languageCatalog[language.toLowerCase()]!,
                            ),
                      label: languageLabel(language),
                      caption: fixed.contains(language) ? 'De la raza' : null,
                      state: fixed.contains(language)
                          ? SelectionState.locked
                          : state.languages.contains(language)
                          ? SelectionState.selected
                          : state.canPickLanguage(language)
                          ? SelectionState.available
                          : SelectionState.blocked,
                    ),
                ],
              ),
            ],
        ],
      ),
    );
  }
}

/// Languages of the catalog by lower-case name and index ("common", "deep speech").
Map<String, ReferenceEntry> _languagesByName(Map<String, ReferenceEntry>? entries) => {
  for (final e in entries?.values ?? const <ReferenceEntry>[]) ...{
    e.index.toLowerCase(): e,
    e.name.toLowerCase(): e,
  },
};

void _showSkill(BuildContext context, String index, Skill skill) {
  final ability = skillAbilities[index] ?? skill.ability;
  showInfoSheet(
    context,
    key: Key('skill-sheet-$index'),
    title: skillLabel(index, skill.name),
    subtitle: ability == null ? null : 'Característica: ${abilityLabel(ability)}',
    description: skill.description,
  );
}

void _showLanguage(BuildContext context, String language, ReferenceEntry entry) {
  showInfoSheet(
    context,
    key: Key('lang-sheet-$language'),
    title: languageLabel(language),
    description: entry.description,
  );
}
