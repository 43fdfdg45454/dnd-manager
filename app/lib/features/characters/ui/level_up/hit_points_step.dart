import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
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

/// Page 2: the player rolls the hit die at the table and writes the result
/// (1..die); the preview adds the Constitution modifier.
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
        Center(
          child: SizedBox(
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
