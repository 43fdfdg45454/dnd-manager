import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/motion/shake.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../catalog/ui/catalog_detail_links.dart' show DetailInfoButton, openSpellDetail;
import '../../../catalog/ui/condition_sheet.dart';
import '../../dnd5e_characters_controller.dart';
import '../../domain/character_format.dart';
import '../../domain/class_theme.dart';
import '../../domain/combat_math.dart';
import '../../models.dart';
import '../character_tabs.dart' show OverrideMark, titleFromSpellIndex;
import 'combat_support.dart';
import 'concentration_flow.dart';

Dnd5eCharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(dnd5eCharacterControllerProvider(character.id).notifier);

// ---------------------------------------------------------------------------
// Hit points
// ---------------------------------------------------------------------------

/// Hit points of the status header: "20 / 28" (" / 28" edits the maximum),
/// the temporary hit points, the bar and the quick damage / healing controls.
///
/// When the hit points of the same character go down the strip shakes and is
/// tinted with blood ([ShakeAndTint]); when they go up it pulses in moss
/// ([PulseTint]).
class HpStrip extends ConsumerStatefulWidget {
  const HpStrip({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  ConsumerState<HpStrip> createState() => _HpStripState();
}

class _HpStripState extends ConsumerState<HpStrip> {
  final _amount = TextEditingController(text: '1');
  late final _maxTap = TapGestureRecognizer()..onTap = _editMax;

  /// Bumped when the hit points go down (damage) or up (healing).
  int _hits = 0;
  int _heals = 0;

  @override
  void didUpdateWidget(HpStrip oldWidget) {
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
    final c = widget.character;
    final max = c.sheet.hitPointsMax;
    final fraction = max <= 0 ? 0.0 : (c.hitPointsCurrent / max).clamp(0.0, 1.0);
    final tokens = context.tokens;
    final barColor = fraction > .5
        ? tokens.moss
        : fraction > .25
        ? tokens.ember
        : tokens.blood;
    const tintRadius = BorderRadius.all(Radius.circular(8));
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
          child: _strip(context, c, max, fraction, barColor),
        ),
      ),
    );
  }

  Widget _strip(BuildContext context, CharacterDetail c, int max, double fraction, Color barColor) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // The numbers on the left and the temporary hit points chip on the
          // right of the same row, vertically centered; when the two do not
          // fit side by side the chip goes below instead of overflowing.
          Wrap(
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            runSpacing: 2,
            children: [
              Row(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(right: 6),
                    child: Text('PG', style: theme.textTheme.labelLarge),
                  ),
                  Flexible(
                    child: FittedBox(
                      fit: BoxFit.scaleDown,
                      alignment: AlignmentDirectional.centerStart,
                      // Only " / max" reacts to taps: it opens the maximum editor.
                      child: KeyedSubtree(
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
                          style: numericStyle(theme.textTheme.headlineSmall),
                          semanticsLabel: '${c.hitPointsCurrent} de $max puntos de golpe',
                        ),
                      ),
                    ),
                  ),
                  OverrideMark(character: c, field: hitPointsMaxField),
                ],
              ),
              ActionChip(
                key: const Key('temp-hp'),
                visualDensity: VisualDensity.compact,
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                avatar: const AppIcon(AppIcons.shield, size: 16),
                label: Text(
                  'PG temporales: ${c.temporaryHitPoints}',
                  style: numericStyle(theme.textTheme.labelMedium),
                ),
                tooltip: 'PG temporales',
                onPressed: widget.canEdit ? _editTemp : null,
              ),
            ],
          ),
          Row(
            children: [
              Expanded(
                child: LinearProgressIndicator(
                  key: const Key('hp-bar'),
                  value: fraction,
                  minHeight: 10,
                  borderRadius: BorderRadius.circular(5),
                  color: barColor,
                ),
              ),
              const SizedBox(width: 8),
              IconButton.filledTonal(
                key: const Key('hp-minus'),
                tooltip: 'Daño',
                onPressed: widget.canEdit ? _damage : null,
                icon: const AppIcon(AppIcons.splash, size: 24),
              ),
              SizedBox(
                width: 64,
                child: TextField(
                  key: const Key('hp-amount'),
                  controller: _amount,
                  textAlign: TextAlign.center,
                  keyboardType: TextInputType.number,
                  style: numericStyle(theme.textTheme.titleMedium),
                  decoration: const InputDecoration(
                    hintText: 'PG',
                    border: OutlineInputBorder(),
                    isDense: true,
                    contentPadding: EdgeInsets.symmetric(horizontal: 4, vertical: 8),
                  ),
                ),
              ),
              IconButton.filledTonal(
                key: const Key('hp-plus'),
                tooltip: 'Curación',
                onPressed: widget.canEdit ? _heal : null,
                icon: const AppIcon(AppIcons.drop, size: 24),
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
// Death saves
// ---------------------------------------------------------------------------

/// Three success pips and as many failure pips as the sheet's
/// `deathSaveFailuresToDie` (3 unless a feature changes it), with "Tirar";
/// only shown at 0 hit points, in the status header.
class DeathSavesStrip extends ConsumerWidget {
  const DeathSavesStrip({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final toDie = c.sheet.deathSaveFailuresToDie;

    Widget row(String label, String keyPrefix, int count, Color color, bool success) => Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Padding(padding: const EdgeInsets.only(right: 4), child: Text(label)),
        for (var i = 0; i < (success ? 3 : toDie); i++)
          Padding(
            padding: const EdgeInsets.only(right: 2),
            child: InkResponse(
              key: Key('$keyPrefix-$i'),
              radius: 22,
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
                width: 36,
                height: 40,
                child: Center(
                  child: Pip(filled: i < count, color: color, size: 26),
                ),
              ),
            ),
          ),
      ],
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (c.hitPointsCurrent == 0 && c.deathSaveSuccesses < 3 && c.deathSaveFailures < toDie)
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Container(
              key: const Key('death-save-banner'),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
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
        _card(context, ref, c, row),
      ],
    );
  }

  /// Rolls the save with the virtual die and, with write access, applies it:
  /// 10 or more is a success, less a failure, a 1 two failures and a 20
  /// brings the character back with 1 hit point.
  Future<void> _roll(BuildContext context, WidgetRef ref, CharacterDetail c) async {
    Future<void>? applying;
    await rollAndShow(
      context,
      '1d20',
      label: 'Salvación de muerte',
      kind: RollKind.deathSave,
      onResult: (result) {
        final natural = result.d20Value;
        if (!canEdit || natural == null) return;
        final outcome = applyDeathSaveRoll(
          natural: natural,
          successes: c.deathSaveSuccesses,
          failures: c.deathSaveFailures,
          failuresToDie: c.sheet.deathSaveFailuresToDie,
        );
        final controller = _controller(ref, c);
        applying = runCombat(
          context,
          () => controller.patchCombat(
            CombatPatch(
              hitPointsCurrent: outcome.revived ? 1 : null,
              deathSaveSuccesses: outcome.successes,
              deathSaveFailures: outcome.failures,
            ),
          ),
          success: outcome.message,
        );
      },
    );
    await applying;
  }

  Widget _card(
    BuildContext context,
    WidgetRef ref,
    CharacterDetail c,
    Widget Function(String, String, int, Color, bool) row,
  ) {
    return Column(
      key: const Key('death-saves'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Salvaciones de muerte', style: Theme.of(context).textTheme.labelLarge),
        Row(
          children: [
            Expanded(
              child: Wrap(
                spacing: 12,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  row('Éxitos', 'death-success', c.deathSaveSuccesses, context.tokens.moss, true),
                  row('Fallos', 'death-failure', c.deathSaveFailures, context.tokens.blood, false),
                ],
              ),
            ),
            FilledButton.tonal(
              key: const Key('roll-death-save'),
              onPressed: () => _roll(context, ref, c),
              child: const Text('Tirar'),
            ),
          ],
        ),
        if (c.sheet.deathSaveFailuresSource case final String source)
          Text(
            'Mueres con ${c.sheet.deathSaveFailuresToDie} fallos ($source).',
            key: const Key('death-failures-to-die'),
            style: Theme.of(context).textTheme.bodySmall,
          ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Conditions
// ---------------------------------------------------------------------------

/// The states of the character in the status header: the spell it
/// concentrates on (with "Perder", set when a concentration spell is cast),
/// its conditions and exhaustion as chips, and "+" to add a condition.
class ConditionsStrip extends ConsumerWidget {
  const ConditionsStrip({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  Future<void> _add(BuildContext context, WidgetRef ref) async {
    final picked = await showConditionPicker(
      context,
      taken: {for (final k in character.conditions) k.index},
      campaignId: character.campaignId,
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
      for (final cond in ref.watch(conditionsProvider(null)).value ?? const <Condition>[])
        cond.index: cond.name,
    };
    // Concentration is a state of the character too: it goes first, as a chip.
    final concentrating = c.concentratingOnSpellIndex;
    final spellName = concentrating == null
        ? null
        : ref.watch(spellInfoProvider(spellInfoKey([concentrating]))).value?[concentrating]?.name ??
              titleFromSpellIndex(concentrating);
    return Wrap(
      key: const Key('conditions-card'),
      spacing: 6,
      runSpacing: 4,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        if (concentrating != null) ...[
          InputChip(
            key: const Key('concentration-chip'),
            visualDensity: VisualDensity.compact,
            avatar: const AppIcon(AppIcons.anchor, size: 16),
            label: Text('Concentración: $spellName', overflow: TextOverflow.ellipsis),
            onPressed: () => openSpellDetail(context, concentrating),
          ),
          TextButton(
            key: const Key('concentration-lose'),
            style: TextButton.styleFrom(visualDensity: VisualDensity.compact),
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
        for (final cond in c.conditions)
          InputChip(
            key: Key('condition-${cond.index}'),
            visualDensity: VisualDensity.compact,
            label: Text(conditionTitle(cond.index, names[cond.index])),
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
            visualDensity: VisualDensity.compact,
            label: Text(
              '${conditionTitle('exhaustion', names['exhaustion'])} ${c.exhaustionLevel}',
            ),
            deleteButtonTooltipMessage: 'Quitar',
            onPressed: canEdit ? () => _setExhaustion(context, ref) : null,
            onDeleted: canEdit
                ? () => runCombat(
                    context,
                    () => _controller(ref, c).patchCombat(const CombatPatch(exhaustionLevel: 0)),
                  )
                : null,
          ),
        ActionChip(
          key: const Key('condition-add'),
          visualDensity: VisualDensity.compact,
          avatar: const Icon(Icons.add, size: 16),
          label: Text(
            c.conditions.isEmpty && c.exhaustionLevel == 0 && concentrating == null
                ? 'Condición'
                : 'Añadir',
          ),
          tooltip: 'Añadir condición',
          onPressed: canEdit ? () => _add(context, ref) : null,
        ),
      ],
    );
  }
}

/// Opens the [ConditionPicker] in a draggable bottom sheet (like the other long
/// pickers of the app) and returns the picked condition, or null.
Future<Condition?> showConditionPicker(
  BuildContext context, {
  required Set<String> taken,
  String? campaignId,
}) {
  return showModalBottomSheet<Condition>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.7,
      minChildSize: 0.4,
      maxChildSize: 0.95,
      builder: (_, controller) =>
          ConditionPicker(taken: taken, campaignId: campaignId, scrollController: controller),
    ),
  );
}

/// Picks one condition of the catalog that is not in [taken], by its Spanish
/// name in alphabetical order; every row has a [DetailInfoButton] with the
/// rules of the condition (the picker stays open).
class ConditionPicker extends ConsumerWidget {
  const ConditionPicker({super.key, required this.taken, this.campaignId, this.scrollController});

  final Set<String> taken;

  /// The campaign whose catalog offers the conditions (its content packs);
  /// null for the global catalog.
  final String? campaignId;
  final ScrollController? scrollController;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final conditions = ref.watch(conditionsProvider(campaignId));
    final theme = Theme.of(context);
    final Widget body = conditions.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (_, _) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('No se pudo cargar la lista de condiciones.'),
          TextButton(
            onPressed: () => ref.invalidate(conditionsProvider(campaignId)),
            child: const Text('Reintentar'),
          ),
        ],
      ),
      data: (list) {
        // One entry per index (the catalog of a campaign never repeats one,
        // but a repeated index would otherwise show twice).
        final seen = <String>{};
        final available = [
          for (final cond in list)
            if (!taken.contains(cond.index) && seen.add(cond.index))
              (condition: cond, title: conditionTitle(cond.index, cond.name)),
        ]..sort((a, b) => foldForSort(a.title).compareTo(foldForSort(b.title)));
        if (available.isEmpty) {
          return const Padding(
            padding: EdgeInsets.all(16),
            child: Text('No hay más condiciones disponibles.'),
          );
        }
        return ListView.builder(
          key: const Key('condition-picker-list'),
          controller: scrollController,
          itemCount: available.length,
          itemBuilder: (context, i) {
            final (:condition, :title) = available[i];
            return ListTile(
              key: Key('pick-condition-${condition.index}'),
              title: Text(title),
              trailing: DetailInfoButton(
                key: Key('condition-info-${condition.index}'),
                tooltip: 'Ver descripción',
                onPressed: () => showConditionSheet(
                  context,
                  Condition(
                    index: condition.index,
                    name: title,
                    description: condition.description,
                    source: condition.source,
                  ),
                ),
              ),
              onTap: () => Navigator.of(context).pop(condition),
            );
          },
        );
      },
    );
    return SafeArea(
      top: false,
      child: Column(
        key: const Key('condition-picker'),
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 8, 8),
            child: Row(
              children: [
                Expanded(child: Text('Añadir condición', style: theme.textTheme.titleLarge)),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Cancelar'),
                ),
              ],
            ),
          ),
          Expanded(child: body),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Status header
// ---------------------------------------------------------------------------

/// The always visible top of the combat view: hit points with the quick
/// damage and healing, the death saves at 0 hit points and the concentration
/// and conditions. From [wideBreakpoint] on, the hit points go on the left
/// and the death saves and conditions on the right, in one row; narrower, it
/// is one column.
class CombatStatusHeader extends StatelessWidget {
  const CombatStatusHeader({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  /// Width (logical pixels) from which the header lays out as one row.
  static const wideBreakpoint = 720.0;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = character;
    final hp = HpStrip(character: c, canEdit: canEdit);
    final states = <Widget>[
      if (c.hitPointsCurrent == 0) DeathSavesStrip(character: c, canEdit: canEdit),
      ConditionsStrip(character: c, canEdit: canEdit),
    ];
    return DecoratedBox(
      key: const Key('combat-status'),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerLow,
        border: Border(bottom: BorderSide(color: context.tokens.oldGold.withValues(alpha: 0.5))),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        child: LayoutBuilder(
          builder: (context, constraints) => constraints.maxWidth >= wideBreakpoint
              ? Row(
                  key: const Key('combat-status-wide'),
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(flex: 2, child: hp),
                    const SizedBox(width: 12),
                    Expanded(
                      flex: 3,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: states,
                      ),
                    ),
                  ],
                )
              : Column(
                  key: const Key('combat-status-narrow'),
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [hp, ...states],
                ),
        ),
      ),
    );
  }
}

