import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../dice/ui/roll_input_button.dart';
import '../../data/level_up_controller.dart';
import 'level_up_widgets.dart';

/// "+ Con 2 = +7 PG" (or "− Con 1 = …" with a negative modifier); without a
/// valid roll only the Constitution part.
String hitPointsPreview({required int? rolled, required int conModifier}) {
  final con = conModifier >= 0 ? '+ Con $conModifier' : '− Con ${conModifier.abs()}';
  if (rolled == null) return con;
  final total = rolled + conModifier < 1 ? 1 : rolled + conModifier;
  return '$con = +$total PG';
}

/// The fixed hit points the PHB offers instead of rolling: half the die plus
/// one (4, 5, 6 and 7 for d6, d8, d10 and d12).
int fixedHitPoints(int die) => die ~/ 2 + 1;

/// Page 2: the player rolls the hit die at the table and writes the result
/// (1..die), rolls it with the virtual dice or takes the fixed value; the
/// preview adds the Constitution modifier.
class LevelUpHitPointsStep extends ConsumerStatefulWidget {
  const LevelUpHitPointsStep({super.key, required this.characterId});

  final String characterId;

  @override
  ConsumerState<LevelUpHitPointsStep> createState() => _LevelUpHitPointsStepState();
}

class _LevelUpHitPointsStepState extends ConsumerState<LevelUpHitPointsStep> {
  late final TextEditingController _text = TextEditingController(
    text: ref.read(levelUpControllerProvider(widget.characterId)).hitPointsText,
  );

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  void _fill(int value) {
    _text.text = '$value';
    ref.read(levelUpControllerProvider(widget.characterId).notifier).setHitPoints('$value');
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(levelUpControllerProvider(widget.characterId));
    final controller = ref.read(levelUpControllerProvider(widget.characterId).notifier);
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final die = state.hitDie;
    final con = state.plan?.conModifier ?? 0;
    final rolled = state.hitPointsRolled;
    final outOfRange = _text.text.trim().isNotEmpty && rolled == null;
    return LevelUpStepList(
      children: [
        LevelUpHeading('Puntos de golpe', subtitle: 'Tira 1d$die y escribe el resultado'),
        Center(
          child: SizedBox.square(
            dimension: 120,
            child: Stack(
              alignment: Alignment.center,
              children: [
                AppIcon(AppIcons.d20, size: 120, color: tokens.oldGold.withValues(alpha: 0.35)),
                Text(
                  'd$die',
                  key: const Key('levelup-hp-die'),
                  style: theme.textTheme.headlineMedium?.copyWith(
                    color: tokens.oldGold,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const SizedBox(width: 48),
            SizedBox(
              width: 160,
              child: TextField(
                key: const Key('levelup-hp-field'),
                controller: _text,
                keyboardType: TextInputType.number,
                textAlign: TextAlign.center,
                inputFormatters: [
                  FilteringTextInputFormatter.digitsOnly,
                  LengthLimitingTextInputFormatter(2),
                ],
                style: theme.textTheme.displaySmall?.merge(AppTypography.numeric),
                decoration: InputDecoration(
                  hintText: '1-$die',
                  errorText: outOfRange ? 'Entre 1 y $die' : null,
                ),
                onChanged: (value) {
                  controller.setHitPoints(value);
                  setState(() {});
                },
              ),
            ),
            RollInputButton(
              key: const Key('levelup-hp-roll'),
              expression: '1d$die',
              label: state.plan == null
                  ? 'Puntos de golpe'
                  : 'Puntos de golpe (nivel ${state.plan!.targetLevel})',
              onRolled: (total, _) => _fill(total),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Center(
          child: OutlinedButton(
            key: const Key('levelup-hp-fixed'),
            onPressed: () => _fill(fixedHitPoints(die)),
            child: Text('Usar el valor fijo (${fixedHitPoints(die)})'),
          ),
        ),
        Center(
          child: Text(
            'El Manual del jugador permite tomar el valor fijo (mitad del dado + 1) en lugar de tirar.',
            key: const Key('levelup-hp-fixed-hint'),
            textAlign: TextAlign.center,
            style: theme.textTheme.bodySmall?.copyWith(color: tokens.boneMuted),
          ),
        ),
        const SizedBox(height: 16),
        Center(
          child: Text(
            hitPointsPreview(rolled: rolled, conModifier: con),
            key: const Key('levelup-hp-preview'),
            style: theme.textTheme.titleLarge?.copyWith(color: tokens.moss),
          ),
        ),
        const SizedBox(height: 8),
        Center(
          child: Text(
            'Mínimo 1 punto de golpe por nivel.',
            style: theme.textTheme.bodySmall?.copyWith(color: tokens.boneMuted),
          ),
        ),
      ],
    );
  }
}
