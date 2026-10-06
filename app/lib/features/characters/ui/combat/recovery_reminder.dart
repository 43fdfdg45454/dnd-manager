import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models.dart';
import 'combat_support.dart';
import 'panels/panel_support.dart';
import 'panels/wizard.dart' show ArcaneRecoveryDialog;
import 'resources_section.dart' show regularSlots;

/// Arcane Recovery budget (half the wizard level, rounded up) when [character]
/// is a wizard with the resource unused and at least one spent slot of level
/// 5 or lower; null otherwise. Natural Recovery (circle of the land druid) has
/// no server resource or endpoint yet, so it is not offered.
int? arcaneRecoveryBudget(CharacterDetail character) {
  final panel = character.combat.panelOf('wizard');
  if (panel == null) return null;
  final resource =
      findResource(character, 'arcane-recovery', 'arcane recovery') ??
      findResource(character, 'arcane-recovery', 'recuperación arcana');
  final recovery = panelMap(panel.data['arcaneRecovery']);
  final available = resource != null
      ? remainingOf(resource) > 0
      : recovery.isNotEmpty && recovery['used'] != true;
  if (!available) return null;
  final spent = regularSlots(character).any((s) => s.level <= 5 && s.used > 0);
  if (!spent) return null;
  return panelInt(recovery['slotLevelsRecoverable']) ?? (panel.level + 1) ~/ 2;
}

/// Non-blocking SnackBar offered after an approved short rest: "¿Usar
/// Recuperación arcana?" with an action that opens the slot picker. Returns
/// false (showing nothing) when the character cannot use it.
bool showRecoveryReminder(BuildContext context, WidgetRef ref, CharacterDetail character) {
  final budget = arcaneRecoveryBudget(character);
  if (budget == null) return false;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        key: const Key('recovery-reminder'),
        duration: const Duration(seconds: 12),
        content: const Text('¿Usar Recuperación arcana?'),
        action: SnackBarAction(
          label: 'Usar',
          onPressed: () async {
            final levels = await showDialog<List<int>>(
              context: context,
              builder: (_) => ArcaneRecoveryDialog(character: character, budget: budget),
            );
            if (levels == null || levels.isEmpty || !context.mounted) return;
            await runCombat(
              context,
              () => panelController(ref, character).arcaneRecovery(levels),
              success: 'Recuperación arcana aplicada.',
            );
          },
        ),
      ),
    );
  return true;
}
