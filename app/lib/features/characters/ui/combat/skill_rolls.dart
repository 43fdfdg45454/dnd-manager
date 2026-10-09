import 'package:flutter/material.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import 'combat_support.dart';

/// Skills at hand in combat, in this order: the social ones first.
const quickCombatSkills = [
  'deception',
  'persuasion',
  'intimidation',
  'perception',
  'stealth',
  'athletics',
  'acrobatics',
  'insight',
];

/// Asks for normal, advantage or disadvantage; null when dismissed.
Future<AdvantageMode?> pickAdvantageMode(BuildContext context, String title) {
  return showModalBottomSheet<AdvantageMode>(
    context: context,
    showDragHandle: true,
    builder: (sheetContext) => SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          ListTile(title: Text(title)),
          ListTile(
            key: const Key('mode-normal'),
            leading: const AppIcon(AppIcons.d20),
            title: const Text('Normal'),
            onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.normal),
          ),
          ListTile(
            key: const Key('mode-advantage'),
            leading: const Icon(Icons.arrow_upward),
            title: const Text('Con ventaja'),
            onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.advantage),
          ),
          ListTile(
            key: const Key('mode-disadvantage'),
            leading: const Icon(Icons.arrow_downward),
            title: const Text('Con desventaja'),
            onTap: () => Navigator.of(sheetContext).pop(AdvantageMode.disadvantage),
          ),
        ],
      ),
    ),
  );
}

/// Rolls a skill check (d20 + the sheet's value).
Future<void> rollSkill(
  BuildContext context,
  SheetSkill skill, [
  AdvantageMode mode = AdvantageMode.normal,
]) => rollAndShow(
  context,
  d20Expression(skill.value, mode: mode),
  label: skillLabel(skill.index, skill.name),
);

/// Asks for advantage or disadvantage, then rolls the skill.
Future<void> rollSkillWithMode(BuildContext context, SheetSkill skill) async {
  final mode = await pickAdvantageMode(context, skillLabel(skill.index, skill.name));
  if (mode == null || !context.mounted) return;
  await rollSkill(context, skill, mode);
}

/// A row of quick skill rolls for the combat stats card ([quickCombatSkills])
/// and "Todas las habilidades". Tap rolls; long press asks for advantage or
/// disadvantage first. Rolling never writes, so it works for any viewer.
class QuickSkillRolls extends StatelessWidget {
  const QuickSkillRolls({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final skills = character.sheet.skills;
    if (skills.isEmpty) return const SizedBox.shrink();
    final byIndex = {for (final s in skills) s.index: s};
    final quick = [for (final index in quickCombatSkills) ?byIndex[index]];
    return Column(
      key: const Key('quick-skills'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Habilidades', style: Theme.of(context).textTheme.labelMedium),
        const SizedBox(height: 4),
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: [
            for (final skill in quick)
              OutlinedButton(
                key: Key('quick-skill-${skill.index}'),
                style: OutlinedButton.styleFrom(
                  visualDensity: VisualDensity.compact,
                  padding: const EdgeInsets.symmetric(horizontal: 10),
                ),
                onPressed: () => rollSkill(context, skill),
                onLongPress: () => rollSkillWithMode(context, skill),
                child: Text(
                  '${skillLabel(skill.index, skill.name)} ${formatModifier(skill.value)}',
                ),
              ),
            TextButton.icon(
              key: const Key('all-skills'),
              style: TextButton.styleFrom(visualDensity: VisualDensity.compact),
              onPressed: () => showAllSkillsSheet(context, character),
              icon: const Icon(Icons.list, size: 18),
              label: const Text('Todas las habilidades'),
            ),
          ],
        ),
      ],
    );
  }
}

/// Every skill of the sheet in a bottom sheet; tap rolls, long press asks
/// for advantage or disadvantage first.
Future<void> showAllSkillsSheet(BuildContext context, CharacterDetail character) {
  final skills = [...character.sheet.skills]
    ..sort((a, b) => skillLabel(a.index, a.name).compareTo(skillLabel(b.index, b.name)));
  return showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (sheetContext) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.7,
      maxChildSize: 0.95,
      builder: (_, scrollController) => ListView(
        key: const Key('all-skills-sheet'),
        controller: scrollController,
        children: [
          const ListTile(
            title: Text('Habilidades'),
            subtitle: Text('Toca para tirar; mantén pulsado para ventaja o desventaja.'),
          ),
          for (final skill in skills)
            ListTile(
              key: Key('all-skills-${skill.index}'),
              dense: true,
              leading: Icon(
                skill.expertise
                    ? Icons.stars
                    : skill.proficient
                    ? Icons.circle
                    : Icons.radio_button_unchecked,
                size: 18,
                semanticLabel: skill.expertise
                    ? 'Pericia'
                    : skill.proficient
                    ? 'Competente'
                    : 'Sin competencia',
              ),
              title: Text(skillLabel(skill.index, skill.name)),
              subtitle: Text(abilityAbbreviation(skill.ability)),
              trailing: Text(
                formatModifier(skill.value),
                style: numericStyle(Theme.of(sheetContext).textTheme.titleMedium),
              ),
              // Rolls over the page (this sheet stays open for the next roll).
              onTap: () => rollSkill(context, skill),
              onLongPress: () => rollSkillWithMode(context, skill),
            ),
        ],
      ),
    ),
  );
}
