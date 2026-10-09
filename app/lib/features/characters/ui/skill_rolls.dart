import 'package:flutter/material.dart';

import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../../dice/domain/dice_expression.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/models.dart';
import '../domain/character_format.dart';

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
