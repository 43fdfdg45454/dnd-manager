import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/tokens.dart';
import 'motion_settings.dart';

/// Wax seal of a DM message: whole while not [broken]; when [broken] turns on
/// it cracks in two halves that fall apart and fade in [duration], then
/// nothing is drawn (the space is kept). Under reduced motion, or with
/// [enabled] false, it disappears at once. [onBroken] is called when the break
/// finishes.
class SealBreak extends StatefulWidget {
  const SealBreak({
    super.key,
    required this.broken,
    this.size = 40,
    this.color,
    this.enabled = true,
    this.onBroken,
  });

  static const duration = Duration(milliseconds: 600);

  final bool broken;
  final double size;

  /// Wax colour; defaults to [AppTokens.blood].
  final Color? color;
  final bool enabled;
  final VoidCallback? onBroken;

  @override
  State<SealBreak> createState() => _SealBreakState();
}

class _SealBreakState extends State<SealBreak> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: SealBreak.duration,
    value: widget.broken ? 1 : 0,
  );

  @override
  void didUpdateWidget(SealBreak oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.broken == widget.broken) return;
    if (!widget.broken) {
      _controller.value = 0;
    } else if (!widget.enabled || MotionScope.reducedOf(context)) {
      _controller.value = 1;
      _notify();
    } else {
      _controller.forward(from: 0).then((_) => _notify(), onError: (_) {});
    }
  }

  void _notify() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) widget.onBroken?.call();
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final color = widget.color ?? context.tokens.blood;
    return SizedBox.square(
      dimension: widget.size,
      child: CustomPaint(
        painter: WaxSealPainter(
          progress: _controller,
          wax: color,
          stamp: Color.lerp(color, const Color(0xFF000000), 0.35)!,
          shine: Color.lerp(color, const Color(0xFFFFFFFF), 0.3)!,
        ),
      ),
    );
  }
}

/// A wax seal with an irregular rim and a stamped rune; [progress] 0 is
/// whole, 1 is gone, in between the halves split along a zigzag crack.
class WaxSealPainter extends CustomPainter {
  WaxSealPainter({
    required this.progress,
    required this.wax,
    required this.stamp,
    required this.shine,
  }) : super(repaint: progress);

  final Animation<double> progress;
  final Color wax;
  final Color stamp;
  final Color shine;

  /// Radii (fractions of the full radius) of the blob's rim.
  static final _rim = List.generate(16, (i) => 0.9 + math.Random(400 + i).nextDouble() * 0.1);

  @override
  void paint(Canvas canvas, Size size) {
    final t = progress.value;
    if (t >= 1 || size.isEmpty) return;
    final center = size.center(Offset.zero);
    final radius = size.shortestSide / 2;

    if (t <= 0) {
      _seal(canvas, center, radius, 1);
      return;
    }
    final eased = Curves.easeIn.transform(t);
    final alpha = 1 - eased;
    final crack = _crack(center, radius);
    for (final side in const [-1.0, 1.0]) {
      final half = Path()..addPath(crack, Offset.zero);
      final x = side < 0 ? center.dx - radius * 2 : center.dx + radius * 2;
      half
        ..lineTo(x, center.dy + radius * 2)
        ..lineTo(x, center.dy - radius * 2)
        ..close();
      canvas.save();
      canvas.translate(side * radius * 0.5 * eased, radius * 0.6 * eased * eased);
      canvas.translate(center.dx, center.dy);
      canvas.rotate(side * 0.5 * eased);
      canvas.translate(-center.dx, -center.dy);
      canvas.clipPath(half);
      _seal(canvas, center, radius, alpha);
      canvas.restore();
    }
  }

  /// Zigzag from above the seal to below it.
  Path _crack(Offset center, double radius) {
    final path = Path()..moveTo(center.dx + radius * 0.05, center.dy - radius * 2);
    const points = [(0.1, -0.7), (-0.15, -0.3), (0.12, 0.05), (-0.1, 0.4), (0.08, 0.75)];
    for (final (dx, dy) in points) {
      path.lineTo(center.dx + dx * radius, center.dy + dy * radius);
    }
    return path..lineTo(center.dx, center.dy + radius * 2);
  }

  void _seal(Canvas canvas, Offset center, double radius, double alpha) {
    final rim = Path();
    for (var i = 0; i < _rim.length; i++) {
      final angle = 2 * math.pi * i / _rim.length;
      final point = center + Offset(math.cos(angle), math.sin(angle)) * radius * _rim[i];
      i == 0 ? rim.moveTo(point.dx, point.dy) : rim.lineTo(point.dx, point.dy);
    }
    rim.close();
    canvas.drawPath(rim, Paint()..color = wax.withValues(alpha: alpha));
    final stroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..strokeWidth = math.max(1.2, radius * 0.08);
    // Stamped ring and rune.
    canvas.drawCircle(center, radius * 0.66, stroke..color = stamp.withValues(alpha: alpha));
    final r = radius * 0.4;
    canvas.drawLine(center.translate(0, -r), center.translate(0, r), stroke);
    canvas.drawLine(center, center.translate(-r * 0.7, -r * 0.7), stroke);
    canvas.drawLine(center, center.translate(r * 0.7, -r * 0.7), stroke);
    // Highlight on the upper left of the wax.
    canvas.drawArc(
      Rect.fromCircle(center: center, radius: radius * 0.82),
      math.pi * 1.05,
      math.pi * 0.35,
      false,
      stroke
        ..color = shine.withValues(alpha: alpha * 0.8)
        ..strokeWidth = math.max(1.0, radius * 0.06),
    );
  }

  @override
  bool shouldRepaint(WaxSealPainter old) =>
      old.progress != progress || old.wax != wax || old.stamp != stamp || old.shine != shine;
}
