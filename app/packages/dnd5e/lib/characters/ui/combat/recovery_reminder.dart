import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../models.dart';
import 'combat_support.dart';
import 'panels/panel_support.dart';
import 'panels/wizard.dart' show ArcaneRecoveryDialog;
import 'resources_section.dart' show regularSlots;

/// The two short-rest slot recoveries: a wizard's Arcane Recovery and a Circle
/// of the Land druid's Natural Recovery. They share rules (half the class level
/// rounded up in slot levels, none of 6th level or higher, once per day).
enum SlotRecovery {
  arcane(
    classIndex: 'wizard',
    panelKey: 'arcaneRecovery',
    resourceKey: 'arcane-recovery',
    resourceName: 'arcane recovery',
    resourceNameEs: 'recuperación arcana',
    label: 'Recuperación arcana',
    action: 'arcane-recovery',
  ),
  natural(
    classIndex: 'druid',
    panelKey: 'naturalRecovery',
    resourceKey: 'natural-recovery',
    resourceName: 'natural recovery',
    resourceNameEs: 'recuperación natural',
    label: 'Recuperación natural',
    action: 'natural-recovery',
  );

  const SlotRecovery({
    required this.classIndex,
    required this.panelKey,
    required this.resourceKey,
    required this.resourceName,
    required this.resourceNameEs,
    required this.label,
    required this.action,
  });

  final String classIndex;

  /// Key of the class panel data (`arcaneRecovery`, `naturalRecovery`).
  final String panelKey;
  final String resourceKey;
  final String resourceName;
  final String resourceNameEs;

  /// Spanish name of the feature.
  final String label;

  /// Class action of `POST /characters/{id}/class-actions/{action}`.
  final String action;

  /// Uses the recovery through the server.
  Future<void> use(WidgetRef ref, CharacterDetail character, List<int> slotLevels) {
    final controller = panelController(ref, character);
    return this == arcane
        ? controller.arcaneRecovery(slotLevels)
        : controller.naturalRecovery(slotLevels);
  }
}

/// Budget of [recovery] (half the class level, rounded up) when [character] can
/// use it now: the class panel offers it, it is unused and at least one slot of
/// level 5 or lower is spent; null otherwise.
int? slotRecoveryBudget(CharacterDetail character, SlotRecovery recovery) {
  final panel = character.combat.panelOf(recovery.classIndex);
  if (panel == null) return null;
  final resource =
      findResource(character, recovery.resourceKey, recovery.resourceName) ??
      findResource(character, recovery.resourceKey, recovery.resourceNameEs);
  final data = panelMap(panel.data[recovery.panelKey]);
  final available = resource != null
      ? remainingOf(resource) > 0
      : data.isNotEmpty && data['used'] != true;
  if (!available) return null;
  final spent = regularSlots(character).any((s) => s.level <= 5 && s.used > 0);
  if (!spent) return null;
  return panelInt(data['slotLevelsRecoverable']) ?? (panel.level + 1) ~/ 2;
}

/// Arcane Recovery budget (see [slotRecoveryBudget]).
int? arcaneRecoveryBudget(CharacterDetail character) =>
    slotRecoveryBudget(character, SlotRecovery.arcane);

/// Natural Recovery budget (see [slotRecoveryBudget]).
int? naturalRecoveryBudget(CharacterDetail character) =>
    slotRecoveryBudget(character, SlotRecovery.natural);

/// Non-blocking SnackBar offered after an approved short rest: "¿Usar
/// Recuperación arcana?" (or "natural") with an action that opens the slot
/// picker. Returns false (showing nothing) when the character cannot use any.
bool showRecoveryReminder(BuildContext context, WidgetRef ref, CharacterDetail character) {
  for (final recovery in SlotRecovery.values) {
    final budget = slotRecoveryBudget(character, recovery);
    if (budget == null) continue;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          key: const Key('recovery-reminder'),
          duration: const Duration(seconds: 12),
          content: Text('¿Usar ${recovery.label}?'),
          action: SnackBarAction(
            label: 'Usar',
            onPressed: () async {
              final levels = await showDialog<List<int>>(
                context: context,
                builder: (_) => ArcaneRecoveryDialog(
                  character: character,
                  budget: budget,
                  title: recovery.label,
                ),
              );
              if (levels == null || levels.isEmpty || !context.mounted) return;
              await runCombat(
                context,
                () => recovery.use(ref, character, levels),
                success: '${recovery.label} aplicada.',
              );
            },
          ),
        ),
      );
    return true;
  }
  return false;
}
