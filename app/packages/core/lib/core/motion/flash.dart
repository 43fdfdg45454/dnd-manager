import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/tokens.dart';
import 'one_shot.dart';

/// Radial flash over [child] (Castigo divino): a golden burst that grows from
/// [center] and fades in [duration]. Plays when [trigger] changes; see
/// [OneShotMotion].
class RadialFlash extends OneShotMotion {
  const RadialFlash({
    super.key,
    required this.child,
    super.trigger,
    super.enabled,
    super.playOnMount,
    super.onCompleted,
    this.color,
    this.center = Alignment.center,
  });

  static const duration = Duration(milliseconds: 400);

  final Widget child;

  /// Defaults to [AppTokens.oldGold].
  final Color? color;
  final Alignment center;

  @override
  State<RadialFlash> createState() => _RadialFlashState();
}

class _RadialFlashState extends OneShotMotionState<RadialFlash> {
  @override
  Duration get duration => RadialFlash.duration;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        widget.child,
        Positioned.fill(
          child: IgnorePointer(
            child: CustomPaint(
              painter: RadialFlashPainter(
                progress: controller,
                color: widget.color ?? context.tokens.oldGold,
                center: widget.center,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// A burst of [color] whose radius grows and alpha fades with [progress];
/// draws nothing at 0 and 1.
class RadialFlashPainter extends CustomPainter {
  RadialFlashPainter({required this.progress, required this.color, required this.center})
    : super(repaint: progress);

  final Animation<double> progress;
  final Color color;
  final Alignment center;

  @override
  void paint(Canvas canvas, Size size) {
    final t = progress.value;
    if (t <= 0 || t >= 1 || size.isEmpty) return;
    final origin = center.alongSize(size);
    final reach = math.sqrt(size.width * size.width + size.height * size.height) * 0.6;
    final radius = math.max(1.0, reach * Curves.easeOutCubic.transform(t));
    final alpha = (1 - t) * 0.85;
    canvas.drawCircle(
      origin,
      radius,
      Paint()
        ..shader = RadialGradient(
          colors: [
            color.withValues(alpha: alpha),
            color.withValues(alpha: alpha * 0.4),
            color.withValues(alpha: 0),
          ],
          stops: const [0, 0.6, 1],
        ).createShader(Rect.fromCircle(center: origin, radius: radius)),
    );
    // Thin bright ring at the front of the burst.
    canvas.drawCircle(
      origin,
      radius * 0.92,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..color = color.withValues(alpha: alpha),
    );
  }

  @override
  bool shouldRepaint(RadialFlashPainter old) =>
      old.progress != progress || old.color != color || old.center != center;
}
