import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_support.dart';
import '../../../../systems/dnd5e/characters/dnd5e_characters_controller.dart';

/// Name of the spell [index]: the catalog's, or one derived from the index when
/// the catalog cannot answer.
Future<String> concentrationSpellName(WidgetRef ref, String index) async {
  try {
    final info = await ref.read(spellInfoProvider(spellInfoKey([index])).future);
    return info[index]?.name ?? titleFromSpellIndex(index);
  } catch (_) {
    return titleFromSpellIndex(index);
  }
}

/// Handles what a damage did to the concentration of [characterId]:
///
/// * with a Constitution save to make (`concentrationCheckDc`), asks
///   "¿Superaste la salvación de Constitución (CD N)?"; "No" ends the
///   concentration through the server. When the Constitution save bonus is
///   known ([constitutionSave], or the loaded sheet of the character),
///   "Tirar salvación" rolls d20 + bonus and answers against the DC;
/// * when the concentration ended by itself (0 hit points), warns
///   "Pierdes la concentración en X".
///
/// [spellName] is looked up in the catalog when it is not given.
Future<void> resolveDamageOutcome(
  BuildContext context,
  WidgetRef ref,
  DamageOutcome outcome, {
  required String characterId,
  String? characterName,
  int? constitutionSave,
}) async {
  final spellIndex = outcome.concentratingOn;
  if (spellIndex == null || !outcome.affectsConcentration) return;
  final spell = await concentrationSpellName(ref, spellIndex);
  if (!context.mounted) return;
  final who = characterName == null ? '' : '$characterName: ';

  if (outcome.concentrationEnded) {
    showCombatMessage(context, '${who}Pierdes la concentración en $spell.');
    return;
  }
  final dc = outcome.concentrationCheckDc;
  if (dc == null) return;
  final saveBonus =
      constitutionSave ??
      ref.read(characterControllerProvider(characterId)).value?.sheet.savingThrows['con']?.value;
  final answer = await showDialog<_ConcentrationAnswer>(
    context: context,
    barrierDismissible: false,
    builder: (dialogContext) => AlertDialog(
      key: const Key('concentration-save-dialog'),
      title: Text(characterName == null ? 'Concentración' : 'Concentración de $characterName'),
      content: Text(
        '¿Superaste la salvación de Constitución (CD $dc)?\n'
        'Estás concentrado en $spell.',
      ),
      actions: [
        if (saveBonus != null)
          TextButton(
            key: const Key('concentration-save-roll'),
            onPressed: () => Navigator.of(dialogContext).pop(_ConcentrationAnswer.roll),
            child: const Text('Tirar salvación'),
          ),
        TextButton(
          key: const Key('concentration-save-no'),
          onPressed: () => Navigator.of(dialogContext).pop(_ConcentrationAnswer.failed),
          child: const Text('No'),
        ),
        FilledButton(
          key: const Key('concentration-save-yes'),
          onPressed: () => Navigator.of(dialogContext).pop(_ConcentrationAnswer.passed),
          child: const Text('Sí'),
        ),
      ],
    ),
  );
  if (!context.mounted) return;
  var passed = answer != _ConcentrationAnswer.failed;
  if (answer == _ConcentrationAnswer.roll && saveBonus != null) {
    final result = await rollAndShow(
      context,
      d20Expression(saveBonus),
      label: 'Salvación de Constitución (CD $dc)',
    );
    if (result == null || !context.mounted) return;
    passed = result.total >= dc;
    if (passed) {
      showCombatMessage(
        context,
        '$who${result.total} contra CD $dc: mantienes la concentración en $spell.',
      );
      return;
    }
  }
  if (passed) return;
  // Nothing else may be watching this character (the DM's table lists
  // summaries), so keep its controller alive while the request runs.
  final keepAlive = ref.listenManual(characterControllerProvider(characterId), (_, _) {});
  try {
    await runCombat(
      context,
      () => ref.read(dnd5eCharacterControllerProvider(characterId).notifier).setConcentration(null),
      success: '${who}Pierdes la concentración en $spell.',
    );
  } finally {
    keepAlive.close();
  }
}

enum _ConcentrationAnswer { passed, failed, roll }

/// True when starting to concentrate on [newSpellIndex] is fine: the character
/// is not concentrating, already concentrates on it, or the player confirms
/// "Dejarás de concentrarte en X".
Future<bool> confirmReplaceConcentration(
  BuildContext context,
  WidgetRef ref,
  CharacterDetail character,
  String newSpellIndex,
) async {
  final current = character.concentratingOnSpellIndex;
  if (current == null || current == newSpellIndex) return true;
  final name = await concentrationSpellName(ref, current);
  if (!context.mounted) return false;
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      key: const Key('concentration-replace-dialog'),
      title: const Text('Cambiar de concentración'),
      content: Text('Dejarás de concentrarte en $name.'),
      actions: [
        TextButton(
          key: const Key('concentration-replace-cancel'),
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: const Text('Cancelar'),
        ),
        FilledButton(
          key: const Key('concentration-replace-confirm'),
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: const Text('Continuar'),
        ),
      ],
    ),
  );
  return confirmed ?? false;
}
