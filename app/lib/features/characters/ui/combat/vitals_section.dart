import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import '../../domain/combat_math.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_support.dart';

CharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

// ---------------------------------------------------------------------------
// Hit points
// ---------------------------------------------------------------------------

/// Hit points with the quick damage / healing controls and temporary hit points.
class HpCard extends ConsumerStatefulWidget {
  const HpCard({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  ConsumerState<HpCard> createState() => _HpCardState();
}

class _HpCardState extends ConsumerState<HpCard> {
  final _amount = TextEditingController(text: '1');

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  /// The amount in the field, or null (with a message) when it is not a number.
  int? _readAmount() {
    final value = int.tryParse(_amount.text.trim());
    if (value == null || value < 1) {
      showCombatMessage(context, 'Escribe una cantidad mayor que 0.');
      return null;
    }
    return value;
  }

  Future<void> _damage() async {
    final amount = _readAmount();
    if (amount == null) return;
    final c = widget.character;
    final next = applyDamage(hp: c.hitPointsCurrent, temp: c.temporaryHitPoints, amount: amount);
    await runCombat(
      context,
      () => _controller(ref, c).patchCombat(
        CombatPatch(
          hitPointsCurrent: next.hp == c.hitPointsCurrent ? null : next.hp,
          temporaryHitPoints: next.temp == c.temporaryHitPoints ? null : next.temp,
        ),
      ),
    );
  }

  Future<void> _heal() async {
    final amount = _readAmount();
    if (amount == null) return;
    final c = widget.character;
    final next = applyHealing(hp: c.hitPointsCurrent, max: c.sheet.hitPointsMax, amount: amount);
    // Coming back from 0 clears the death saves.
    final revived = c.hitPointsCurrent == 0 && next > 0;
    await runCombat(
      context,
      () => _controller(ref, c).patchCombat(
        CombatPatch(
          hitPointsCurrent: next,
          deathSaveSuccesses: revived && c.deathSaveSuccesses != 0 ? 0 : null,
          deathSaveFailures: revived && c.deathSaveFailures != 0 ? 0 : null,
        ),
      ),
    );
  }

  Future<void> _editTemp() async {
    final c = widget.character;
    final value = await promptNumber(
      context,
      title: 'PG temporales',
      label: 'PG temporales',
      initial: c.temporaryHitPoints,
    );
    if (value == null || !mounted) return;
    await runCombat(
      context,
      () => _controller(ref, c).patchCombat(CombatPatch(temporaryHitPoints: value)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = widget.character;
    final max = c.sheet.hitPointsMax;
    final fraction = max <= 0 ? 0.0 : (c.hitPointsCurrent / max).clamp(0.0, 1.0);
    final barColor = fraction > .5
        ? Colors.green.shade600
        : fraction > .25
        ? Colors.orange.shade700
        : theme.colorScheme.error;
    return CombatCard(
      title: 'Puntos de golpe',
      trailing: ActionChip(
        key: const Key('temp-hp'),
        avatar: const Icon(Icons.shield_outlined, size: 18),
        label: Text('PG temp.: ${c.temporaryHitPoints}'),
        onPressed: widget.canEdit ? _editTemp : null,
      ),
      child: Column(
        children: [
          Text(
            '${c.hitPointsCurrent} / $max',
            key: const Key('combat-hp'),
            style: theme.textTheme.displaySmall?.copyWith(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          LinearProgressIndicator(
            key: const Key('hp-bar'),
            value: fraction,
            minHeight: 12,
            borderRadius: BorderRadius.circular(6),
            color: barColor,
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              IconButton.filledTonal(
                key: const Key('hp-minus'),
                tooltip: 'Daño',
                iconSize: 32,
                onPressed: widget.canEdit ? _damage : null,
                icon: const Icon(Icons.remove),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: TextField(
                  key: const Key('hp-amount'),
                  controller: _amount,
                  textAlign: TextAlign.center,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Daño / curación',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              IconButton.filledTonal(
                key: const Key('hp-plus'),
                tooltip: 'Curación',
                iconSize: 32,
                onPressed: widget.canEdit ? _heal : null,
                icon: const Icon(Icons.add),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Armor class, initiative, speed, inspiration, concentration
// ---------------------------------------------------------------------------

class StatsCard extends ConsumerWidget {
  const StatsCard({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = character;
    final sheet = c.sheet;
    final concentrating = c.concentratingOnSpellIndex;
    final spellName = concentrating == null
        ? null
        : ref.watch(spellInfoProvider(spellInfoKey([concentrating]))).value?[concentrating]?.name ??
              titleFromSpellIndex(concentrating);

    Widget tile(String key, String label, String value, {Widget? action}) => Card(
      key: Key('combat-$key'),
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(label, style: theme.textTheme.labelMedium),
            Text(value, style: theme.textTheme.headlineSmall),
            ?action,
          ],
        ),
      ),
    );

    return CombatCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              tile('ac', 'CA', '${sheet.armorClass}'),
              tile(
                'initiative',
                'Iniciativa',
                formatModifier(sheet.initiative),
                action: TextButton(
                  key: const Key('roll-initiative'),
                  onPressed: () =>
                      rollAndShow(context, d20Expression(sheet.initiative), label: 'Iniciativa'),
                  child: const Text('Tirar'),
                ),
              ),
              tile('speed', 'Velocidad', '${sheet.speed} pies'),
              FilterChip(
                key: const Key('inspiration'),
                avatar: const Icon(Icons.auto_awesome, size: 18),
                label: const Text('Inspiración'),
                selected: c.inspiration,
                onSelected: canEdit
                    ? (value) => runCombat(
                        context,
                        () => _controller(ref, c).patchCombat(CombatPatch(inspiration: value)),
                      )
                    : null,
              ),
            ],
          ),
          if (concentrating != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Row(
                children: [
                  Flexible(
                    child: Chip(
                      key: const Key('concentration-chip'),
                      avatar: const Icon(Icons.psychology_outlined, size: 18),
                      label: Text('Concentración: $spellName', overflow: TextOverflow.ellipsis),
                    ),
                  ),
                  const SizedBox(width: 8),
                  TextButton(
                    key: const Key('concentration-lose'),
                    onPressed: canEdit
                        ? () => runCombat(
                            context,
                            () => _controller(ref, c).setConcentration(null),
                            success: 'Concentración perdida.',
                          )
                        : null,
                    child: const Text('Perder'),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Death saves
// ---------------------------------------------------------------------------

/// Three success and three failure circles; only shown at 0 hit points.
class DeathSavesCard extends ConsumerWidget {
  const DeathSavesCard({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;

    Widget row(String label, String keyPrefix, int count, Color color, bool success) => Row(
      children: [
        SizedBox(width: 72, child: Text(label)),
        for (var i = 0; i < 3; i++)
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: InkResponse(
              key: Key('$keyPrefix-$i'),
              radius: 28,
              onTap: canEdit
                  ? () => runCombat(
                      context,
                      () => _controller(ref, c).patchCombat(
                        success
                            ? CombatPatch(deathSaveSuccesses: toggleDeathSave(count, i))
                            : CombatPatch(deathSaveFailures: toggleDeathSave(count, i)),
                      ),
                    )
                  : null,
              child: SizedBox(
                width: 44,
                height: 44,
                child: Icon(
                  i < count ? Icons.circle : Icons.circle_outlined,
                  color: color,
                  size: 32,
                ),
              ),
            ),
          ),
      ],
    );

    return CombatCard(
      key: const Key('death-saves'),
      title: 'Salvaciones de muerte',
      trailing: TextButton(
        key: const Key('roll-death-save'),
        onPressed: () => rollAndShow(context, '1d20', label: 'Salvación de muerte'),
        child: const Text('Tirar'),
      ),
      child: Column(
        children: [
          row('Éxitos', 'death-success', c.deathSaveSuccesses, Colors.green.shade700, true),
          row(
            'Fallos',
            'death-failure',
            c.deathSaveFailures,
            Theme.of(context).colorScheme.error,
            false,
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Conditions
// ---------------------------------------------------------------------------

class ConditionsCard extends ConsumerWidget {
  const ConditionsCard({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  Future<void> _add(BuildContext context, WidgetRef ref) async {
    final picked = await showDialog<Condition>(
      context: context,
      builder: (_) => _ConditionPicker(taken: {for (final k in character.conditions) k.index}),
    );
    if (picked == null || !context.mounted) return;
    if (picked.index == 'exhaustion') {
      await _setExhaustion(context, ref);
      return;
    }
    await runCombat(
      context,
      () => _controller(ref, character).patchCombat(
        CombatPatch(
          conditions: [
            ...character.conditions,
            CharacterCondition(index: picked.index),
          ],
        ),
      ),
    );
  }

  Future<void> _setExhaustion(BuildContext context, WidgetRef ref) async {
    final level = await showDialog<int>(
      context: context,
      builder: (dialogContext) => SimpleDialog(
        title: const Text('Nivel de agotamiento'),
        children: [
          for (var level = 1; level <= 6; level++)
            SimpleDialogOption(
              key: Key('exhaustion-level-$level'),
              onPressed: () => Navigator.of(dialogContext).pop(level),
              child: Text(
                level == character.exhaustionLevel ? 'Nivel $level (actual)' : 'Nivel $level',
              ),
            ),
        ],
      ),
    );
    if (level == null || !context.mounted) return;
    await runCombat(
      context,
      () => _controller(ref, character).patchCombat(CombatPatch(exhaustionLevel: level)),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final names = {
      for (final cond in ref.watch(conditionsProvider).value ?? const <Condition>[])
        cond.index: cond.name,
    };
    return CombatCard(
      title: 'Condiciones',
      child: Wrap(
        spacing: 8,
        runSpacing: 4,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          for (final cond in c.conditions)
            InputChip(
              key: Key('condition-${cond.index}'),
              label: Text(names[cond.index] ?? titleFromSpellIndex(cond.index)),
              deleteButtonTooltipMessage: 'Quitar',
              onDeleted: canEdit
                  ? () => runCombat(
                      context,
                      () => _controller(ref, c).patchCombat(
                        CombatPatch(
                          conditions: [
                            for (final other in c.conditions)
                              if (other.index != cond.index) other,
                          ],
                        ),
                      ),
                    )
                  : null,
            ),
          if (c.exhaustionLevel > 0)
            InputChip(
              key: const Key('condition-exhaustion'),
              label: Text('${names['exhaustion'] ?? 'Exhaustion'} ${c.exhaustionLevel}'),
              deleteButtonTooltipMessage: 'Quitar',
              onPressed: canEdit ? () => _setExhaustion(context, ref) : null,
              onDeleted: canEdit
                  ? () => runCombat(
                      context,
                      () => _controller(ref, c).patchCombat(const CombatPatch(exhaustionLevel: 0)),
                    )
                  : null,
            ),
          if (c.conditions.isEmpty && c.exhaustionLevel == 0) const Text('Sin condiciones.'),
          ActionChip(
            key: const Key('condition-add'),
            avatar: const Icon(Icons.add, size: 18),
            label: const Text('Añadir'),
            onPressed: canEdit ? () => _add(context, ref) : null,
          ),
        ],
      ),
    );
  }
}

class _ConditionPicker extends ConsumerWidget {
  const _ConditionPicker({required this.taken});

  final Set<String> taken;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final conditions = ref.watch(conditionsProvider);
    return AlertDialog(
      title: const Text('Añadir condición'),
      content: SizedBox(
        width: double.maxFinite,
        child: conditions.when(
          loading: () =>
              const SizedBox(height: 80, child: Center(child: CircularProgressIndicator())),
          error: (_, _) => Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('No se pudo cargar la lista de condiciones.'),
              TextButton(
                onPressed: () => ref.invalidate(conditionsProvider),
                child: const Text('Reintentar'),
              ),
            ],
          ),
          data: (list) {
            final available = [
              for (final cond in list)
                if (!taken.contains(cond.index)) cond,
            ];
            if (available.isEmpty) return const Text('No hay más condiciones disponibles.');
            return ListView(
              shrinkWrap: true,
              children: [
                for (final cond in available)
                  ListTile(
                    key: Key('pick-condition-${cond.index}'),
                    title: Text(cond.name),
                    onTap: () => Navigator.of(context).pop(cond),
                  ),
              ],
            );
          },
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
      ],
    );
  }
}
