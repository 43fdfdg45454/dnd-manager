import 'dart:math' as math;
import 'dart:ui' show PointMode;

import 'package:flutter/widgets.dart';

import '../theme/tokens.dart';
import 'one_shot.dart';

/// Short rest approved: a campfire flares up over [child] — logs, flames that
/// grow and die down and sparks rising — in [duration]. Plays when [trigger]
/// changes; see [OneShotMotion]. Never takes touches.
class CampfireBurst extends OneShotMotion {
  const CampfireBurst({
    super.key,
    required this.child,
    super.trigger,
    super.enabled,
    super.playOnMount,
    super.onCompleted,
  });

  static const duration = Duration(milliseconds: 1200);

  final Widget child;

  @override
  State<CampfireBurst> createState() => _CampfireBurstState();
}

class _CampfireBurstState extends OneShotMotionState<CampfireBurst> {
  @override
  Duration get duration => CampfireBurst.duration;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return _Overlay(
      painter: CampfirePainter(
        progress: controller,
        flame: tokens.ember,
        core: Color.lerp(tokens.ember, const Color(0xFFFFFFFF), 0.6)!,
        logs: tokens.oldGold,
      ),
      child: widget.child,
    );
  }
}

/// Long rest approved: the night falls over [child] and a crescent moon
/// crosses it among twinkling stars in [duration]. Plays when [trigger]
/// changes; see [OneShotMotion]. Never takes touches.
class MoonPass extends OneShotMotion {
  const MoonPass({
    super.key,
    required this.child,
    super.trigger,
    super.enabled,
    super.playOnMount,
    super.onCompleted,
  });

  static const duration = Duration(milliseconds: 1500);

  final Widget child;

  @override
  State<MoonPass> createState() => _MoonPassState();
}

class _MoonPassState extends OneShotMotionState<MoonPass> {
  @override
  Duration get duration => MoonPass.duration;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return _Overlay(
      painter: MoonPassPainter(
        progress: controller,
        night: const Color(0xFF0B0D1A),
        moon: Color.lerp(tokens.oldGold, const Color(0xFFFFFFFF), 0.75)!,
        stars: tokens.oldGold,
      ),
      child: widget.child,
    );
  }
}

class _Overlay extends StatelessWidget {
  const _Overlay({required this.painter, required this.child});

  final CustomPainter painter;
  final Widget child;

  @override
  Widget build(BuildContext context) => Stack(
    children: [
      child,
      Positioned.fill(
        child: IgnorePointer(child: CustomPaint(painter: painter)),
      ),
    ],
  );
}

/// 0 → 1 → 0 over the play: fades in the first 15 % and out the last 25 %.
double _envelope(double t) {
  if (t <= 0 || t >= 1) return 0;
  if (t < 0.15) return t / 0.15;
  if (t > 0.75) return (1 - t) / 0.25;
  return 1;
}

/// Campfire of a [CampfireBurst] at the bottom centre of the area.
class CampfirePainter extends CustomPainter {
  CampfirePainter({
    required this.progress,
    required this.flame,
    required this.core,
    required this.logs,
  }) : super(repaint: progress);

  final Animation<double> progress;
  final Color flame;
  final Color core;
  final Color logs;

  static final _sparks = List.generate(12, (i) {
    final random = math.Random(100 + i);
    return (
      x: random.nextDouble() * 2 - 1,
      speed: 0.6 + random.nextDouble() * 0.6,
      delay: random.nextDouble() * 0.4,
    );
  });

  @override
  void paint(Canvas canvas, Size size) {
    final t = progress.value;
    final fade = _envelope(t);
    if (fade == 0 || size.isEmpty) return;
    final unit = math.min(120.0, math.min(size.width, size.height) * 0.4);
    final base = Offset(size.width / 2, size.height - unit * 0.25);

    // Warm glow behind the fire.
    canvas.drawCircle(
      base.translate(0, -unit * 0.3),
      unit * 0.9,
      Paint()
        ..shader = RadialGradient(
          colors: [
            flame.withValues(alpha: 0.35 * fade),
            flame.withValues(alpha: 0),
          ],
        ).createShader(Rect.fromCircle(center: base.translate(0, -unit * 0.3), radius: unit * 0.9)),
    );

    // Crossed logs.
    final log = Paint()
      ..color = logs.withValues(alpha: fade)
      ..strokeWidth = unit * 0.08
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(
      base.translate(-unit * 0.45, 0),
      base.translate(unit * 0.45, -unit * 0.15),
      log,
    );
    canvas.drawLine(
      base.translate(-unit * 0.45, -unit * 0.15),
      base.translate(unit * 0.45, 0),
      log,
    );

    // Flames: they grow, flicker and die down.
    final grow = math.sin(math.pi * t);
    final flicker = math.sin(t * math.pi * 14) * 0.08;
    for (final (dx, scale) in const [(-0.18, 0.7), (0.18, 0.75), (0.0, 1.0)]) {
      final h = unit * (0.9 * scale) * (grow + flicker).clamp(0.0, 1.2);
      final w = unit * 0.18 * scale;
      final foot = base.translate(unit * dx, -unit * 0.08);
      canvas.drawPath(_tongue(foot, w, h), Paint()..color = flame.withValues(alpha: 0.85 * fade));
      canvas.drawPath(
        _tongue(foot, w * 0.5, h * 0.55),
        Paint()..color = core.withValues(alpha: 0.9 * fade),
      );
    }

    // Sparks rising.
    final spark = Paint()..strokeCap = StrokeCap.round;
    for (final s in _sparks) {
      final local = ((t - s.delay) / (1 - s.delay)).clamp(0.0, 1.0);
      if (local == 0 || local == 1) continue;
      final position = base.translate(
        s.x * unit * 0.4 + math.sin(local * math.pi * 3 + s.x) * 6,
        -unit * 0.2 - local * unit * 1.4 * s.speed,
      );
      spark
        ..color = core.withValues(alpha: (1 - local) * fade)
        ..strokeWidth = 2.5;
      canvas.drawPoints(PointMode.points, [position], spark);
    }
  }

  static Path _tongue(Offset foot, double w, double h) => Path()
    ..moveTo(foot.dx - w, foot.dy)
    ..lineTo(foot.dx - w * 0.6, foot.dy - h * 0.5)
    ..lineTo(foot.dx, foot.dy - h)
    ..lineTo(foot.dx + w * 0.6, foot.dy - h * 0.5)
    ..lineTo(foot.dx + w, foot.dy)
    ..close();

  @override
  bool shouldRepaint(CampfirePainter old) =>
      old.progress != progress || old.flame != flame || old.core != core || old.logs != logs;
}

/// Night tint, stars and a crescent moon crossing an arc for a [MoonPass].
class MoonPassPainter extends CustomPainter {
  MoonPassPainter({
    required this.progress,
    required this.night,
    required this.moon,
    required this.stars,
  }) : super(repaint: progress);

  final Animation<double> progress;
  final Color night;
  final Color moon;
  final Color stars;

  static final _stars = List.generate(14, (i) {
    final random = math.Random(200 + i);
    return (x: random.nextDouble(), y: random.nextDouble() * 0.6, phase: random.nextDouble());
  });

  @override
  void paint(Canvas canvas, Size size) {
    final t = progress.value;
    final fade = _envelope(t);
    if (fade == 0 || size.isEmpty) return;
    canvas.drawRect(Offset.zero & size, Paint()..color = night.withValues(alpha: 0.45 * fade));

    final star = Paint()
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 2;
    for (final s in _stars) {
      final twinkle = 0.5 + 0.5 * math.sin(2 * math.pi * (t * 2 + s.phase));
      star.color = stars.withValues(alpha: fade * twinkle);
      canvas.drawPoints(PointMode.points, [Offset(s.x * size.width, s.y * size.height)], star);
    }

    final radius = math.min(36.0, math.min(size.width, size.height) * 0.12);
    final eased = Curves.easeInOut.transform(t);
    final center = Offset(
      -radius + (size.width + radius * 2) * eased,
      size.height * 0.4 - math.sin(math.pi * eased) * size.height * 0.2,
    );
    final disc = Path()..addOval(Rect.fromCircle(center: center, radius: radius));
    final bite = Path()
      ..addOval(
        Rect.fromCircle(center: center.translate(radius * 0.45, -radius * 0.2), radius: radius),
      );
    final crescent = Path.combine(PathOperation.difference, disc, bite);
    canvas.drawPath(
      crescent,
      Paint()
        ..color = moon.withValues(alpha: 0.5 * fade)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6),
    );
    canvas.drawPath(crescent, Paint()..color = moon.withValues(alpha: fade));
  }

  @override
  bool shouldRepaint(MoonPassPainter old) =>
      old.progress != progress || old.night != night || old.moon != moon || old.stars != stars;
}
