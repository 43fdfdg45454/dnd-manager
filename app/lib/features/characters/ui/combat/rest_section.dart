import 'package:flutter/foundation.dart' show mapEquals;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../campaigns/ui/confirm_dialog.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_state.dart';
import 'combat_support.dart';
import 'recovery_reminder.dart';
import 'rest_celebration.dart';
import '../../../../systems/dnd5e/characters/dnd5e_characters_controller.dart';

/// "Descansos". The DM (and the Owner) rests the character directly: "Descanso
/// corto" asks for the hit dice to spend and "Descanso largo" confirms. A
/// player cannot rest by themselves: "Pedir descanso corto" (choosing the hit
/// dice, with the expected healing) and "Pedir descanso largo" send a request
/// to the DM, and while it is pending a card says "Esperando al DM" with a
/// "Cancelar" button. When the character is refreshed without the pending
/// request and with different hit points, hit dice or exhaustion, the DM
/// approved it ("Descanso aprobado") and the campfire or the moon plays over
/// the screen ([showRestCelebration]).
class RestSection extends ConsumerStatefulWidget {
  const RestSection({super.key, required this.character, required this.canEdit, this.isDm = false});

  final CharacterDetail character;
  final bool canEdit;

  /// The viewer is a DM or the Owner of the campaign: rests apply directly.
  final bool isDm;

  @override
  ConsumerState<RestSection> createState() => _RestSectionState();
}

class _RestSectionState extends ConsumerState<RestSection> {
  CharacterDetail get _character => widget.character;

  Dnd5eCharacterController get _controller =>
      ref.read(dnd5eCharacterControllerProvider(_character.id).notifier);

  CharacterController get _core => ref.read(characterControllerProvider(_character.id).notifier);

  // -- Direct rests (DM) ------------------------------------------------------

  Future<void> _shortRest() async {
    final spent = await showDialog<Map<String, int>>(
      context: context,
      builder: (_) => ShortRestDialog(character: _character),
    );
    if (spent == null || !mounted) return;
    await runCombat(
      context,
      () => _controller.shortRest(hitDice: spent),
      success: 'Descanso corto realizado.',
    );
  }

  Future<void> _longRest() async {
    final confirmed = await confirmAction(
      context,
      title: 'Descanso largo',
      message:
          'Se recuperarán los puntos de golpe, los espacios de conjuro y los recursos que '
          'se recargan con un descanso largo.',
      confirmLabel: 'Descansar',
    );
    if (!confirmed || !mounted) return;
    final rage = ref.read(rageControllerProvider(_character.id).notifier);
    final done = await runCombat(
      context,
      () => _controller.longRest(),
      success: 'Descanso largo realizado.',
    );
    if (done) rage.end();
  }

  // -- Requests (player) ------------------------------------------------------

  Future<void> _requestShort() async {
    final spent = await showModalBottomSheet<Map<String, int>>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => RestRequestSheet(character: _character),
    );
    if (spent == null || !mounted) return;
    await runCombat(
      context,
      () => _core.requestRest(RestKind.short, hitDice: spent),
      success: 'Petición de descanso corto enviada al DM.',
    );
  }

  Future<void> _requestLong() async {
    final confirmed = await confirmAction(
      context,
      title: 'Pedir descanso largo',
      message:
          'El DM recibirá la petición. Al aprobarla se recuperarán los puntos de golpe, los '
          'espacios de conjuro, los recursos de descanso largo y la mitad de los dados de golpe.',
      confirmLabel: 'Pedir',
    );
    if (!confirmed || !mounted) return;
    await runCombat(
      context,
      () => _core.requestRest(RestKind.long),
      success: 'Petición de descanso largo enviada al DM.',
    );
  }

  Future<void> _cancelRequest() async {
    _cancelling = true;
    try {
      await runCombat(
        context,
        () => _core.cancelRestRequest(),
        success: 'Petición de descanso cancelada.',
      );
    } finally {
      _cancelling = false;
    }
  }

  /// Set while the player withdraws their own request (no notice then).
  bool _cancelling = false;

  /// The pending request went away: say what happened.
  void _onCharacterChanged(
    AsyncValue<CharacterDetail>? previous,
    AsyncValue<CharacterDetail> next,
  ) {
    final before = previous?.value;
    final after = next.value;
    if (before == null || after == null || next.isLoading) return;
    if (before.pendingRest == null || after.pendingRest != null || _cancelling) return;
    final changed =
        before.hitPointsCurrent != after.hitPointsCurrent ||
        before.exhaustionLevel != after.exhaustionLevel ||
        !mapEquals(before.hitDiceUsed, after.hitDiceUsed);
    if (changed && before.pendingRest!.kind == RestKind.long) {
      ref.read(rageControllerProvider(after.id).notifier).end();
    }
    if (changed) showRestCelebration(context, before.pendingRest!.kind);
    if (changed &&
        before.pendingRest!.kind == RestKind.short &&
        showRecoveryReminder(context, ref, after)) {
      return;
    }
    showCombatMessage(
      context,
      changed ? 'Descanso aprobado' : 'La petición de descanso ya no está pendiente.',
    );
  }

  @override
  Widget build(BuildContext context) {
    if (widget.isDm) return _directRests();
    ref.listen(characterControllerProvider(_character.id), _onCharacterChanged);
    final pending = _character.pendingRest;
    return CombatCard(
      title: 'Descansos',
      child: pending == null ? _requestButtons() : _pendingCard(pending),
    );
  }

  Widget _directRests() {
    final canEdit = widget.canEdit;
    return CombatCard(
      title: 'Descansos',
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton.icon(
              key: const Key('rest-short'),
              onPressed: canEdit ? _shortRest : null,
              icon: const AppIcon(AppIcons.campfire, size: 20),
              label: const Text('Descanso corto'),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: FilledButton.icon(
              key: const Key('rest-long'),
              onPressed: canEdit ? _longRest : null,
              icon: const AppIcon(AppIcons.moon, size: 20),
              label: const Text('Descanso largo'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _requestButtons() {
    final canEdit = widget.canEdit;
    return Row(
      children: [
        Expanded(
          child: OfflineAware(
            builder: (context, canWrite) => OutlinedButton.icon(
              key: const Key('rest-request-short'),
              onPressed: canEdit && canWrite ? _requestShort : null,
              icon: const AppIcon(AppIcons.campfire, size: 20),
              label: const Text('Pedir descanso corto', textAlign: TextAlign.center),
            ),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: OfflineAware(
            builder: (context, canWrite) => FilledButton.icon(
              key: const Key('rest-request-long'),
              onPressed: canEdit && canWrite ? _requestLong : null,
              icon: const AppIcon(AppIcons.moon, size: 20),
              label: const Text('Pedir descanso largo', textAlign: TextAlign.center),
            ),
          ),
        ),
      ],
    );
  }

  Widget _pendingCard(PendingRest pending) {
    final canEdit = widget.canEdit;
    return Row(
      key: const Key('rest-pending'),
      children: [
        AppIcon(
          pending.kind == RestKind.short ? AppIcons.campfire : AppIcons.moon,
          color: context.tokens.gold,
        ),
        const SizedBox(width: 10),
        Expanded(child: Text('Esperando al DM · ${pending.description}')),
        const SizedBox(width: 8),
        OfflineAware(
          builder: (context, canWrite) => TextButton(
            key: const Key('rest-cancel'),
            onPressed: canEdit && canWrite ? _cancelRequest : null,
            child: const Text('Cancelar'),
          ),
        ),
      ],
    );
  }
}

String _className(CharacterDetail character, String classIndex) {
  for (final c in character.classes) {
    if (c.classIndex == classIndex) return c.className;
  }
  return titleFromSpellIndex(classIndex);
}

/// One row per class with its hit dice: "Fighter (d10) · Quedan 3 de 3" and
/// the minus / plus buttons. [spent] holds the dice chosen per class index.
class _HitDicePicker extends StatelessWidget {
  const _HitDicePicker({required this.character, required this.spent, required this.onChanged});

  final CharacterDetail character;
  final Map<String, int> spent;
  final void Function(String classIndex, int count) onChanged;

  @override
  Widget build(BuildContext context) {
    final dice = character.sheet.hitDice;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (dice.isEmpty) const Text('Este personaje no tiene dados de golpe.'),
        for (final d in dice)
          Row(
            key: Key('hit-dice-${d.classIndex}'),
            children: [
              Expanded(
                child: Text(
                  '${_className(character, d.classIndex)} (d${d.die})\n'
                  'Quedan ${d.remaining} de ${d.total}',
                ),
              ),
              IconButton(
                key: Key('hit-dice-${d.classIndex}-minus'),
                tooltip: 'Menos',
                onPressed: (spent[d.classIndex] ?? 0) > 0
                    ? () => onChanged(d.classIndex, spent[d.classIndex]! - 1)
                    : null,
                icon: const Icon(Icons.remove_circle_outline),
              ),
              SizedBox(
                width: 24,
                child: Text(
                  '${spent[d.classIndex] ?? 0}',
                  textAlign: TextAlign.center,
                  style: numericStyle(Theme.of(context).textTheme.bodyMedium),
                ),
              ),
              IconButton(
                key: Key('hit-dice-${d.classIndex}-plus'),
                tooltip: 'Más',
                onPressed: (spent[d.classIndex] ?? 0) < d.remaining
                    ? () => onChanged(d.classIndex, (spent[d.classIndex] ?? 0) + 1)
                    : null,
                icon: const Icon(Icons.add_circle_outline),
              ),
            ],
          ),
      ],
    );
  }
}

/// Healing a short rest gives on average ("2d10 + 4"): the dice spent of each
/// class plus the Constitution modifier once per die. Null without dice.
String? expectedShortRestHealing(CharacterDetail character, Map<String, int> spent) {
  final parts = <String>[];
  var total = 0;
  for (final d in character.sheet.hitDice) {
    final count = spent[d.classIndex] ?? 0;
    if (count <= 0) continue;
    parts.add('${count}d${d.die}');
    total += count;
  }
  if (parts.isEmpty) return null;
  final bonus = total * (character.sheet.abilities['con']?.modifier ?? 0);
  final dice = parts.join(' + ');
  if (bonus == 0) return dice;
  return bonus > 0 ? '$dice + $bonus' : '$dice - ${-bonus}';
}

/// Bottom sheet of "Pedir descanso corto": how many hit dice of each class to
/// spend, with the expected healing. Resolves to the dice by class index (only
/// classes with at least one); the request goes out when the DM approves.
class RestRequestSheet extends StatefulWidget {
  const RestRequestSheet({super.key, required this.character});

  final CharacterDetail character;

  @override
  State<RestRequestSheet> createState() => _RestRequestSheetState();
}

class _RestRequestSheetState extends State<RestRequestSheet> {
  final Map<String, int> _spent = {};

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final healing = expectedShortRestHealing(widget.character, _spent);
    return SafeArea(
      child: SingleChildScrollView(
        key: const Key('rest-request-sheet'),
        padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Pedir descanso corto', style: theme.textTheme.titleLarge),
            const SizedBox(height: 8),
            const Text('¿Cuántos dados de golpe quieres gastar para recuperar puntos de golpe?'),
            const SizedBox(height: 8),
            _HitDicePicker(
              character: widget.character,
              spent: _spent,
              onChanged: (classIndex, count) => setState(() => _spent[classIndex] = count),
            ),
            const SizedBox(height: 8),
            Text(
              healing == null
                  ? 'Sin dados de golpe: solo se recargarán los recursos de descanso corto.'
                  : 'Curación esperada: $healing',
              key: const Key('rest-request-healing'),
              style: theme.textTheme.bodyMedium,
            ),
            const SizedBox(height: 4),
            Text(
              'El descanso se aplica cuando el DM lo apruebe.',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Cancelar'),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  key: const Key('rest-request-short-confirm'),
                  onPressed: () => Navigator.of(context).pop({
                    for (final e in _spent.entries)
                      if (e.value > 0) e.key: e.value,
                  }),
                  child: const Text('Pedir descanso'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Lets the DM pick how many hit dice of each class to spend. Resolves to
/// the dice spent by class index (only classes with at least one).
class ShortRestDialog extends StatefulWidget {
  const ShortRestDialog({super.key, required this.character});

  final CharacterDetail character;

  @override
  State<ShortRestDialog> createState() => _ShortRestDialogState();
}

class _ShortRestDialogState extends State<ShortRestDialog> {
  final Map<String, int> _spent = {};

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Descanso corto'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('¿Cuántos dados de golpe se gastan para recuperar puntos de golpe?'),
            const SizedBox(height: 8),
            _HitDicePicker(
              character: widget.character,
              spent: _spent,
              onChanged: (classIndex, count) => setState(() => _spent[classIndex] = count),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('rest-short-confirm'),
          onPressed: () => Navigator.of(context).pop({
            for (final e in _spent.entries)
              if (e.value > 0) e.key: e.value,
          }),
          child: const Text('Descansar'),
        ),
      ],
    );
  }
}
