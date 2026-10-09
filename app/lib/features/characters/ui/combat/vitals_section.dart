import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/motion/shake.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/textures.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/stat_value.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../dice/domain/dice_expression.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart';
import '../../domain/class_theme.dart';
import '../../domain/combat_math.dart';
import '../character_tabs.dart' show OverrideMark, titleFromSpellIndex;
import 'combat_support.dart';
import 'concentration_flow.dart';
import 'skill_rolls.dart';

CharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

// ---------------------------------------------------------------------------
// Hit points
// ---------------------------------------------------------------------------

/// Hit points with the quick damage / healing controls and temporary hit points.
///
/// When the hit points of the same character go down the card shakes and is
/// tinted with blood ([ShakeAndTint]); when they go up it pulses in moss
/// ([PulseTint]).
class HpCard extends ConsumerStatefulWidget {
  const HpCard({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  ConsumerState<HpCard> createState() => _HpCardState();
}

class _HpCardState extends ConsumerState<HpCard> {
  final _amount = TextEditingController(text: '1');
  late final _maxTap = TapGestureRecognizer()..onTap = _editMax;

  /// Bumped when the hit points go down (damage) or up (healing).
  int _hits = 0;
  int _heals = 0;

  @override
  void didUpdateWidget(HpCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    final before = oldWidget.character;
    final after = widget.character;
    if (before.id != after.id) return;
    if (after.hitPointsCurrent < before.hitPointsCurrent) {
      _hits++;
    } else if (after.hitPointsCurrent > before.hitPointsCurrent) {
      _heals++;
    }
  }

  @override
  void dispose() {
    _amount.dispose();
    _maxTap.dispose();
    super.dispose();
  }

  /// Edits the maximum hit points as the `hitPointsMax` override (merged with
  /// the character's other overrides, since the list is replaced as a whole).
  /// The DM applies it directly; a player's edit of an active character goes
  /// to the DM for approval.
  Future<void> _editMax() async {
    if (!widget.canEdit) return;
    final c = widget.character;
    final current = c.overrideOf(hitPointsMaxField);
    final choice = await showDialog<HpMaxChoice>(
      context: context,
      builder: (_) => HpMaxDialog(value: c.sheet.hitPointsMax, overridden: current != null),
    );
    if (choice == null || !mounted) return;
    final others = [
      for (final o in c.overrides)
        if (o.field != hitPointsMaxField) o,
    ];
    final List<CharacterOverride> overrides;
    switch (choice) {
      case HpMaxReset():
        if (current == null) return;
        overrides = others;
      case HpMaxValue(:final value):
        if (current?.value == value || (current == null && value == c.sheet.hitPointsMax)) {
          showCombatMessage(context, 'No hay cambios que guardar.');
          return;
        }
        overrides = [
          ...others,
          CharacterOverride(field: hitPointsMaxField, value: value, note: current?.note),
        ];
    }
    SheetSaveResult? result;
    final done = await runCombat(context, () async {
      result = await _controller(ref, c).saveSheet(SheetPatch(overrides: overrides));
    });
    if (!done || !mounted) return;
    showCombatMessage(context, switch (result) {
      PendingApproval() => 'Enviado al DM para aprobación',
      _ when choice is HpMaxReset => 'PG máximos: vuelven al cálculo.',
      _ => 'PG máximos actualizados.',
    });
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
    // The server applies the damage (temporary hit points first) and says what
    // it meant for the concentration.
    DamageOutcome? outcome;
    final done = await runCombat(context, () async {
      outcome = await _controller(ref, c).applyDamage(amount);
    });
    if (!done || outcome == null || !mounted) return;
    await resolveDamageOutcome(context, ref, outcome!, characterId: c.id);
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
    final tokens = context.tokens;
    final barColor = fraction > .5
        ? tokens.moss
        : fraction > .25
        ? tokens.ember
        : tokens.blood;
    final bigNumber = numericStyle(theme.textTheme.displaySmall);
    const tintRadius = BorderRadius.all(Radius.circular(RuneCard.corner));
    return ClassAccent(
      classIndex: mainClassIndex(c),
      child: ShakeAndTint(
        key: const Key('hp-shake'),
        trigger: _hits == 0 ? null : _hits,
        borderRadius: tintRadius,
        child: PulseTint(
          key: const Key('hp-pulse'),
          trigger: _heals == 0 ? null : _heals,
          borderRadius: tintRadius,
          child: _card(context, c, max, fraction, barColor, bigNumber),
        ),
      ),
    );
  }

  Widget _card(
    BuildContext context,
    CharacterDetail c,
    int max,
    double fraction,
    Color barColor,
    TextStyle? bigNumber,
  ) {
    final theme = Theme.of(context);
    return CombatCard(
      title: 'Puntos de golpe',
      trailing: ActionChip(
        key: const Key('temp-hp'),
        avatar: const AppIcon(AppIcons.shield, size: 18),
        label: Text(
          'PG temp.: ${c.temporaryHitPoints}',
          style: numericStyle(theme.textTheme.labelLarge),
        ),
        onPressed: widget.canEdit ? _editTemp : null,
      ),
      child: Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              // Only " / max" reacts to taps: it opens the maximum editor.
              KeyedSubtree(
                key: const Key('hp-max-edit'),
                child: Text.rich(
                  TextSpan(
                    children: [
                      TextSpan(text: '${c.hitPointsCurrent}'),
                      TextSpan(
                        text: ' / $max',
                        recognizer: widget.canEdit ? _maxTap : null,
                        mouseCursor: widget.canEdit ? SystemMouseCursors.click : null,
                      ),
                    ],
                  ),
                  key: const Key('combat-hp'),
                  style: bigNumber,
                  semanticsLabel: '${c.hitPointsCurrent} de $max puntos de golpe',
                ),
              ),
              OverrideMark(character: c, field: hitPointsMaxField),
            ],
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
                icon: const AppIcon(AppIcons.splash, size: 32),
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
                icon: const AppIcon(AppIcons.drop, size: 32),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Override field of the maximum hit points.
const hitPointsMaxField = 'hitPointsMax';

/// What [HpMaxDialog] resolves to.
sealed class HpMaxChoice {
  const HpMaxChoice();
}

/// Save [value] as the override.
class HpMaxValue extends HpMaxChoice {
  const HpMaxValue(this.value);

  final int value;
}

/// Remove the override ("Volver al cálculo").
class HpMaxReset extends HpMaxChoice {
  const HpMaxReset();
}

/// Number field for the maximum hit points with "Guardar" and, when the value
/// is overridden, "Volver al cálculo".
class HpMaxDialog extends StatefulWidget {
  const HpMaxDialog({super.key, required this.value, required this.overridden});

  final int value;
  final bool overridden;

  @override
  State<HpMaxDialog> createState() => _HpMaxDialogState();
}

class _HpMaxDialogState extends State<HpMaxDialog> {
  late final _controller = TextEditingController(text: '${widget.value}');
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _save() {
    final value = int.tryParse(_controller.text.trim());
    if (value == null || value < 1 || value > 999) {
      setState(() => _error = 'Introduce un número entre 1 y 999.');
      return;
    }
    Navigator.of(context).pop(HpMaxValue(value));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('PG máximos'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            key: const Key('hp-max-field'),
            controller: _controller,
            autofocus: true,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(labelText: 'PG máximos', errorText: _error),
            onSubmitted: (_) => _save(),
          ),
          const SizedBox(height: 8),
          Text(
            widget.overridden
                ? 'Valor fijado a mano. "Volver al cálculo" usa el de clase y nivel.'
                : 'Fija un valor a mano en lugar del calculado.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        if (widget.overridden)
          TextButton(
            key: const Key('hp-max-reset'),
            onPressed: () => Navigator.of(context).pop(const HpMaxReset()),
            child: const Text('Volver al cálculo'),
          ),
        FilledButton(key: const Key('hp-max-save'), onPressed: _save, child: const Text('Guardar')),
      ],
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

    Widget tile(
      String key,
      String label,
      String value, {
      Widget? action,
      required String breakdownKey,
      String? totalText,
    }) => StoneCard(
      key: Key('combat-$key'),
      margin: EdgeInsets.zero,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label, style: theme.textTheme.labelMedium),
          StatValue(
            statKey: breakdownKey,
            title: label,
            text: value,
            totalText: totalText,
            breakdown: sheet.breakdown(breakdownKey),
            style: numericStyle(theme.textTheme.headlineSmall),
          ),
          ?action,
        ],
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
              tile('ac', 'CA', '${sheet.armorClass}', breakdownKey: 'armorClass'),
              tile(
                'initiative',
                'Iniciativa',
                formatModifier(sheet.initiative),
                breakdownKey: 'initiative',
                action: TextButton(
                  key: const Key('roll-initiative'),
                  onPressed: () =>
                      rollAndShow(context, d20Expression(sheet.initiative), label: 'Iniciativa'),
                  child: const Text('Tirar'),
                ),
              ),
              tile('speed', 'Velocidad', '${sheet.speed} pies', breakdownKey: 'speed'),
              tile(
                'perception',
                'Percepción pasiva',
                '${sheet.passivePerception}',
                breakdownKey: 'passivePerception',
              ),
              tile(
                'proficiency',
                'Competencia',
                formatModifier(sheet.proficiencyBonus),
                breakdownKey: 'proficiencyBonus',
              ),
              FilterChip(
                key: const Key('inspiration'),
                avatar: const AppIcon(AppIcons.sparkles, size: 18),
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
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: QuickSkillRolls(character: c),
          ),
          if (concentrating != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Row(
                children: [
                  Flexible(
                    child: Chip(
                      key: const Key('concentration-chip'),
                      avatar: const AppIcon(AppIcons.anchor, size: 18),
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
                child: Center(
                  child: Pip(filled: i < count, color: color, size: 32),
                ),
              ),
            ),
          ),
      ],
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (c.hitPointsCurrent == 0 && c.deathSaveSuccesses < 3 && c.deathSaveFailures < 3)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Container(
              key: const Key('death-save-banner'),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: context.tokens.blood.withValues(alpha: 0.18),
                border: Border.all(color: context.tokens.blood),
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Row(
                children: [
                  Icon(Icons.edit_note),
                  SizedBox(width: 8),
                  Expanded(child: Text('Anota tu salvación contra muerte')),
                ],
              ),
            ),
          ),
        _card(context, c, row),
      ],
    );
  }

  Widget _card(
    BuildContext context,
    CharacterDetail c,
    Widget Function(String, String, int, Color, bool) row,
  ) {
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
          row('Éxitos', 'death-success', c.deathSaveSuccesses, context.tokens.moss, true),
          row('Fallos', 'death-failure', c.deathSaveFailures, context.tokens.blood, false),
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
      builder: (_) => ConditionPickerDialog(taken: {for (final k in character.conditions) k.index}),
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

/// Picks one condition of the SRD list that is not in [taken].
class ConditionPickerDialog extends ConsumerWidget {
  const ConditionPickerDialog({super.key, required this.taken});

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
