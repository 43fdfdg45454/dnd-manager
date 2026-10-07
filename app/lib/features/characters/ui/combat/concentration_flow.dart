import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_support.dart';

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
///   concentration through the server;
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
  final passed = await showDialog<bool>(
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
        TextButton(
          key: const Key('concentration-save-no'),
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: const Text('No'),
        ),
        FilledButton(
          key: const Key('concentration-save-yes'),
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: const Text('Sí'),
        ),
      ],
    ),
  );
  if (passed != false || !context.mounted) return;
  // Nothing else may be watching this character (the DM's table lists
  // summaries), so keep its controller alive while the request runs.
  final keepAlive = ref.listenManual(characterControllerProvider(characterId), (_, _) {});
  try {
    await runCombat(
      context,
      () => ref.read(characterControllerProvider(characterId).notifier).setConcentration(null),
      success: '${who}Pierdes la concentración en $spell.',
    );
  } finally {
    keepAlive.close();
  }
}

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
