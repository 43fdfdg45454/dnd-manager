import 'package:flutter/material.dart';

import '../../../catalog/ui/detail_widgets.dart' show SectionTitle;
import '../../data/models.dart';
import '../../domain/character_format.dart' show abilityAbbreviation;
import '../../domain/class_theme.dart';

/// What a level choice picked, in Spanish: "Defensa", "+1 Fue, +1 Des",
/// "Grappler (+1 Fue)", "Mage Hand (sustituye a Light)", "Golpe sereno
/// (2 Ki)" for options with a cost ([costOf]).
String describeCharacterChoice(
  CharacterChoice choice, {
  CharacterOptionCost? Function(String index)? costOf,
}) {
  String withCost(String index, String name) {
    final cost = costOf?.call(index);
    return cost == null ? name : '$name (${cost.label})';
  }

  if (choice.feat != null) {
    final ability = choice.ability;
    final name = withCost(choice.feat!.index, choice.feat!.name);
    return ability == null ? name : '$name (+1 ${abilityAbbreviation(ability)})';
  }
  if (choice.asi.isNotEmpty) {
    return [
      for (final key in abilityKeys)
        if ((choice.asi[key] ?? 0) > 0) '+${choice.asi[key]} ${abilityAbbreviation(key)}',
    ].join(', ');
  }
  final names = choice.selected.map((s) => withCost(s.index, s.name)).join(', ');
  final replaced = choice.replaced.map((r) => r.name).join(', ');
  if (replaced.isEmpty) return names.isEmpty ? '—' : names;
  return names.isEmpty ? 'Sustituye a $replaced' : '$names (sustituye a $replaced)';
}

/// "Elecciones" of the sheet: the choices made when levelling up, grouped by
/// class level, with the picked names.
class CharacterChoicesSection extends StatelessWidget {
  const CharacterChoicesSection({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final choices = [...character.choices]
      ..sort((a, b) {
        final byLevel = a.level.compareTo(b.level);
        return byLevel != 0 ? byLevel : a.classIndex.compareTo(b.classIndex);
      });
    final multiclass = character.classes.length > 1;
    final groups = <String, List<CharacterChoice>>{};
    for (final choice in choices) {
      final className =
          classThemes[choice.classIndex]?.labelEs ??
          character.classes
              .where((c) => c.classIndex == choice.classIndex)
              .map((c) => c.className)
              .firstOrNull ??
          choice.classIndex;
      final title = choice.level == 0
          ? 'Raza y trasfondo'
          : multiclass
          ? '$className · nivel ${choice.level}'
          : 'Nivel ${choice.level}';
      groups.putIfAbsent(title, () => []).add(choice);
    }
    final theme = Theme.of(context);
    return Column(
      key: const Key('sheet-choices'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Elecciones'),
        if (choices.isEmpty)
          Text('Aún no has hecho elecciones al subir de nivel.', style: theme.textTheme.bodyMedium),
        for (final entry in groups.entries) ...[
          Padding(
            padding: const EdgeInsets.only(top: 4, bottom: 2),
            child: Text(entry.key, style: theme.textTheme.labelLarge),
          ),
          for (final choice in entry.value)
            ListTile(
              key: Key('sheet-choice-${choice.level}-${choice.key}'),
              dense: true,
              contentPadding: EdgeInsets.zero,
              title: Text(choice.name),
              subtitle: Text(describeCharacterChoice(choice, costOf: character.optionCost)),
            ),
        ],
      ],
    );
  }
}
