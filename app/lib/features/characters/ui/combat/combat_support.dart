import 'package:flutter/material.dart';

import '../../../../core/motion/motion_settings.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/textures.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../../core/ui/action_type.dart';
import '../../../campaigns/ui/feedback.dart';

/// Spanish message for a failed combat action. Network failures read
/// "Sin conexión" (nothing changed); a 400 shows the server's reason when it
/// sends one (e.g. arcane recovery over the limit).
String describeCombatError(Object error, {Map<int, String> byStatus = const {}}) {
  final base = describeCharacterError(error, byStatus: byStatus);
  if (base == networkErrorMessage) return 'Sin conexión. No se ha cambiado nada.';
  final detail = problemDetail(error);
  if (detail != null && !byStatus.containsKey(400)) return detail;
  return base;
}

/// [runAction] with the combat error texts. Returns true when the action completed.
Future<bool> runCombat(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
}) => runAction(context, action, success: success, errors: errors, describe: describeCombatError);

/// Shows a short message in a SnackBar.
void showCombatMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

/// Asks for a whole number; resolves to null when cancelled.
Future<int?> promptNumber(
  BuildContext context, {
  required String title,
  required String label,
  int initial = 0,
  int min = 0,
  int? max,
  String confirmLabel = 'Aceptar',
}) {
  final controller = TextEditingController(text: '$initial');
  return showDialog<int>(
    context: context,
    builder: (dialogContext) {
      String? error;
      return StatefulBuilder(
        builder: (context, setState) {
          void submit() {
            final value = int.tryParse(controller.text.trim());
            if (value == null || value < min || (max != null && value > max)) {
              setState(
                () => error = max == null
                    ? 'Introduce un número de $min en adelante.'
                    : 'Introduce un número entre $min y $max.',
              );
              return;
            }
            Navigator.of(dialogContext).pop(value);
          }

          return AlertDialog(
            title: Text(title),
            content: TextField(
              key: const Key('number-field'),
              controller: controller,
              autofocus: true,
              keyboardType: TextInputType.number,
              decoration: InputDecoration(labelText: label, errorText: error),
              onSubmitted: (_) => submit(),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                key: const Key('number-confirm'),
                onPressed: submit,
                child: Text(confirmLabel),
              ),
            ],
          );
        },
      );
    },
  );
}

/// A row of rune pips, [filled] of [total] lit. Tapping and long-pressing the
/// whole row (a big target for the thumb) call [onTap] and [onLongPress].
///
/// With [animated] (the default) a pip that goes out is crossed by a stroke
/// ([Pip.strokeDuration]); under reduced motion it just goes out.
class PipRow extends StatelessWidget {
  const PipRow({
    super.key,
    required this.total,
    required this.filled,
    this.onTap,
    this.onLongPress,
    this.semanticLabel,
    this.animated = true,
  });

  final int total;
  final int filled;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final String? semanticLabel;

  /// Whether spent pips play the stroke-off animation.
  final bool animated;

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme.primary;
    // The ink hugs the pips, with the same padding on every side, so the pips
    // sit centred in the highlight. A parent that would stretch it (a tight
    // `Expanded`) wraps it in an `Align` instead.
    return Semantics(
      label: semanticLabel,
      child: InkWell(
        borderRadius: BorderRadius.circular(24),
        onTap: onTap,
        onLongPress: onLongPress,
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48),
          child: Padding(
            padding: const EdgeInsets.all(8),
            child: Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 8,
              runSpacing: 8,
              children: [
                for (var i = 0; i < total; i++)
                  Pip(filled: i < filled, color: color, animated: animated),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// One rune pip of a [PipRow] (also the death saves): a carved diamond, lit
/// with [color] while [filled]. When it goes from filled to empty a stroke
/// crosses it while the light fades ([strokeDuration]), unless [animated] is
/// false or motion is reduced ([MotionScope]).
class Pip extends StatefulWidget {
  const Pip({
    super.key,
    required this.filled,
    required this.color,
    this.size = 24,
    this.animated = true,
  });

  static const strokeDuration = Duration(milliseconds: 200);

  final bool filled;
  final Color color;
  final double size;
  final bool animated;

  @override
  State<Pip> createState() => _PipState();
}

class _PipState extends State<Pip> with SingleTickerProviderStateMixin {
  /// 0 → 1 while the stroke crosses a pip that went out; 1 at rest.
  late final AnimationController _stroke = AnimationController(
    vsync: this,
    duration: Pip.strokeDuration,
    value: 1,
  );

  @override
  void didUpdateWidget(Pip oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.filled && !widget.filled && widget.animated && !MotionScope.reducedOf(context)) {
      _stroke.forward(from: 0);
    } else if (widget.filled && _stroke.value != 1) {
      _stroke.value = 1;
    }
  }

  @override
  void dispose() {
    _stroke.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox.square(
      dimension: widget.size,
      child: CustomPaint(
        painter: PipPainter(
          filled: widget.filled,
          color: widget.color,
          stroke: _stroke,
          rim: context.tokens.oldGold,
        ),
      ),
    );
  }
}

/// Paints a [Pip]: a diamond with a cut rim and, when lit, a filled core.
/// While [stroke] runs (an empty pip that was just spent) the core fades and a
/// diagonal stroke is drawn across it.
class PipPainter extends CustomPainter {
  PipPainter({required this.filled, required this.color, required this.stroke, required this.rim})
    : super(repaint: stroke);

  final bool filled;
  final Color color;
  final Animation<double> stroke;

  /// Colour of the faint inner rim.
  final Color rim;

  /// Whether the stroke-off is currently drawn (for tests).
  bool get spending => !filled && stroke.value < 1;

  Path _diamond(Offset c, double r) => Path()
    ..moveTo(c.dx, c.dy - r)
    ..lineTo(c.dx + r, c.dy)
    ..lineTo(c.dx, c.dy + r)
    ..lineTo(c.dx - r, c.dy)
    ..close();

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    final c = size.center(Offset.zero);
    final r = size.shortestSide / 2 - 1.5;
    final outline = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeJoin = StrokeJoin.miter
      ..color = color;
    canvas.drawPath(_diamond(c, r), outline);
    final t = stroke.value;
    final light = filled ? 1.0 : (t < 1 ? 1 - t : 0.0);
    if (light > 0) {
      canvas.drawPath(_diamond(c, r - 4), Paint()..color = color.withValues(alpha: light));
      canvas.drawPath(
        _diamond(c, r - 4),
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1
          ..color = rim.withValues(alpha: 0.5 * light),
      );
    }
    if (spending) {
      // A stroke from the upper left to the lower right that fades at the end.
      final from = c + Offset(-r * 0.75, -r * 0.75);
      final to = c + Offset(r * 0.75, r * 0.75);
      final reach = Curves.easeOut.transform((t / 0.7).clamp(0.0, 1.0));
      final alpha = t < 0.7 ? 1.0 : (1 - t) / 0.3;
      canvas.drawLine(
        from,
        Offset.lerp(from, to, reach)!,
        Paint()
          ..strokeWidth = 2.5
          ..strokeCap = StrokeCap.round
          ..color = color.withValues(alpha: alpha),
      );
    }
  }

  @override
  bool shouldRepaint(PipPainter old) =>
      old.filled != filled || old.color != color || old.stroke != stroke || old.rim != rim;
}

/// A titled card that groups a section of the combat view, in the rune card
/// style; [title] in the display font, numbers in [AppTypography.numeric].
/// [actionKind] adds its [ActionTypeChip] under the title.
class CombatCard extends StatelessWidget {
  const CombatCard({super.key, this.title, this.trailing, this.actionKind, required this.child});

  final String? title;
  final Widget? trailing;

  /// What using the card's feature costs on the turn (SRD text); null hides it.
  final ActionKind? actionKind;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return RuneCard(
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      seed: title?.hashCode ?? key?.hashCode,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Expanded(child: Text(title!, style: theme.textTheme.titleMedium)),
                  ?trailing,
                ],
              ),
            ),
          if (actionKind != null)
            Padding(padding: const EdgeInsets.only(bottom: 8), child: ActionTypeChip(actionKind!)),
          child,
        ],
      ),
    );
  }
}

/// Padding of the [SectionHeader]s between the cards of the combat view.
const combatSectionPadding = EdgeInsets.only(top: 16, bottom: 4);

/// [style] with the tabular figures of [AppTypography.numeric], for numbers
/// that change in place.
TextStyle? numericStyle(TextStyle? style) =>
    style == null ? AppTypography.numeric : style.merge(AppTypography.numeric);
