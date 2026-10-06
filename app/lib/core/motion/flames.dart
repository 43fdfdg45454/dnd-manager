import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/tokens.dart';
import 'motion_settings.dart';

/// Flames climbing the edges of [child] (Furia, Ataque temerario): when
/// [active] turns on they rise from the bottom up the sides in [igniteDuration]
/// and then keep a constant ember shimmer; when it turns off they die down.
/// Under reduced motion the flames appear at once and stay still.
///
/// The flames are drawn over [child] inside its bounds and never take touches.
class FlameBorder extends StatefulWidget {
  const FlameBorder({
    super.key,
    required this.child,
    required this.active,
    this.color,
    this.inset = 0,
  });

  static const igniteDuration = Duration(milliseconds: 600);
  static const emberPeriod = Duration(milliseconds: 1600);

  final Widget child;
  final bool active;

  /// Outer colour of the flames; defaults to [AppTokens.ember].
  final Color? color;

  /// Distance of the flames from the edges of [child] (e.g. a card margin).
  final double inset;

  @override
  State<FlameBorder> createState() => _FlameBorderState();
}

class _FlameBorderState extends State<FlameBorder> with TickerProviderStateMixin {
  late final AnimationController _ignite = AnimationController(
    vsync: this,
    duration: FlameBorder.igniteDuration,
    reverseDuration: const Duration(milliseconds: 300),
  );
  late final AnimationController _ember = AnimationController(
    vsync: this,
    duration: FlameBorder.emberPeriod,
  );

  @override
  void initState() {
    super.initState();
    _ignite.addStatusListener((status) {
      if (status == AnimationStatus.dismissed) _ember.stop();
    });
  }

  bool _initialized = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Also runs when reduced motion is toggled.
    _sync(initial: !_initialized);
    _initialized = true;
  }

  @override
  void didUpdateWidget(FlameBorder oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.active != widget.active) _sync();
  }

  void _sync({bool initial = false}) {
    final reduced = MotionScope.reducedOf(context);
    if (widget.active) {
      if (reduced) {
        _ember.stop();
        _ignite.value = 1;
      } else {
        // Already burning when first shown: no ignition, just the embers.
        if (initial && _ignite.value == 0) {
          _ignite.value = 1;
        } else {
          _ignite.forward();
        }
        if (!_ember.isAnimating) _ember.repeat();
      }
    } else {
      if (reduced || initial) {
        _ember.stop();
        _ignite.value = 0;
      } else {
        _ignite.reverse();
      }
    }
  }

  @override
  void dispose() {
    _ignite.dispose();
    _ember.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final color = widget.color ?? context.tokens.ember;
    return CustomPaint(
      foregroundPainter: FlamePainter(
        ignite: _ignite,
        ember: _ember,
        color: color,
        core: Color.lerp(color, const Color(0xFFFFD27A), 0.6)!,
        inset: widget.inset,
      ),
      child: widget.child,
    );
  }
}

/// Paints the flames of a [FlameBorder]: a glowing edge revealed from the
/// bottom by [ignite], with tongues along the sides and the bottom whose height
/// sways with [ember].
class FlamePainter extends CustomPainter {
  FlamePainter({
    required this.ignite,
    required this.ember,
    required this.color,
    required this.core,
    this.inset = 0,
  }) : super(repaint: Listenable.merge([ignite, ember]));

  final Animation<double> ignite;
  final Animation<double> ember;
  final Color color;
  final Color core;
  final double inset;

  static const _spacing = 11.0;

  @override
  void paint(Canvas canvas, Size size) {
    final progress = Curves.easeOut.transform(ignite.value.clamp(0.0, 1.0));
    if (progress == 0 || size.isEmpty) return;
    final rect = (Offset.zero & size).deflate(inset + 1);
    if (rect.isEmpty) return;
    final top = rect.bottom - rect.height * progress;
    final phase = ember.value;

    canvas.save();
    canvas.clipRect(Rect.fromLTRB(rect.left - 2, top - 14, rect.right + 2, rect.bottom + 2));

    // Glowing edge.
    final glow = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 6
      ..color = color.withValues(alpha: 0.3)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 4);
    final edge = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5
      ..color = color.withValues(alpha: 0.9);
    canvas.drawRect(rect, glow);
    canvas.drawRect(rect, edge);

    final outer = Paint()..color = color.withValues(alpha: 0.75);
    final inner = Paint()..color = core.withValues(alpha: 0.85);

    // Tongues up both sides, leaning inwards, smaller near the top.
    var i = 0;
    for (var y = rect.bottom; y > top; y -= _spacing, i++) {
      final fade = ((y - top) / (rect.height * 0.25 + 1)).clamp(0.0, 1.0);
      for (final side in const [-1.0, 1.0]) {
        final x = side < 0 ? rect.left : rect.right;
        final height = _height(phase, i, side) * (0.4 + 0.6 * fade);
        _tongue(canvas, Offset(x, y), height, -side, outer, inner);
      }
    }
    // Tongues along the bottom edge.
    var j = 0;
    for (var x = rect.left + _spacing; x < rect.right - _spacing / 2; x += _spacing, j++) {
      final height = _height(phase, j + 31, 0) * 0.8;
      _tongue(canvas, Offset(x, rect.bottom), height, 0, outer, inner);
    }
    canvas.restore();
  }

  /// Height of tongue [i]: 6–12 px swaying with the ember [phase].
  double _height(double phase, int i, double side) {
    final wave = math.sin(2 * math.pi * (phase + i * 0.23 + side * 0.31));
    return 9 + 3 * wave;
  }

  /// One flame tongue standing on [base]: a pointed shape [height] tall,
  /// leaning by [lean] (−1 left, 1 right) with a lighter core.
  void _tongue(Canvas canvas, Offset base, double height, double lean, Paint outer, Paint inner) {
    Path shape(double h, double w) {
      final tip = base + Offset(lean * w * 0.8, -h);
      return Path()
        ..moveTo(base.dx - w, base.dy)
        ..lineTo(base.dx - w * 0.5 + lean * w * 0.3, base.dy - h * 0.55)
        ..lineTo(tip.dx, tip.dy)
        ..lineTo(base.dx + w * 0.5 + lean * w * 0.3, base.dy - h * 0.55)
        ..lineTo(base.dx + w, base.dy)
        ..close();
    }

    canvas.drawPath(shape(height, 4), outer);
    canvas.drawPath(shape(height * 0.55, 2), inner);
  }

  @override
  bool shouldRepaint(FlamePainter old) =>
      old.ignite != ignite ||
      old.ember != ember ||
      old.color != color ||
      old.core != core ||
      old.inset != inset;
}
