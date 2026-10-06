import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import 'tokens.dart';

/// Very subtle film grain: deterministic dots on a [tile]-sized square that is
/// repeated over the whole area. The dots of each (seed, density, tile) are
/// computed once and cached, so repaints only replay them.
class GrainPainter extends CustomPainter {
  GrainPainter({
    required this.color,
    this.opacity = 0.05,
    this.density = 0.012,
    this.seed = 7,
    this.tile = 128,
  });

  /// Colour of the dots (usually the text colour of the theme).
  final Color color;

  /// Alpha applied to [color].
  final double opacity;

  /// Dots per square pixel.
  final double density;
  final int seed;

  /// Side of the repeated square, in logical pixels.
  final double tile;

  static final _cache = <(int, double, double), Float32List>{};

  /// The cached dots of one tile, as x/y pairs.
  Float32List get points => _cache.putIfAbsent((seed, density, tile), () {
    final random = math.Random(seed);
    final count = (tile * tile * density).round();
    final list = Float32List(count * 2);
    for (var i = 0; i < count; i++) {
      list[i * 2] = random.nextDouble() * tile;
      list[i * 2 + 1] = random.nextDouble() * tile;
    }
    return list;
  });

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    final dots = points;
    final paint = Paint()
      ..color = color.withValues(alpha: opacity)
      ..strokeWidth = 1
      ..strokeCap = StrokeCap.round;
    canvas.save();
    canvas.clipRect(Offset.zero & size);
    for (var y = 0.0; y < size.height; y += tile) {
      for (var x = 0.0; x < size.width; x += tile) {
        canvas.save();
        canvas.translate(x, y);
        canvas.drawRawPoints(ui.PointMode.points, dots, paint);
        canvas.restore();
      }
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(GrainPainter old) =>
      old.color != color ||
      old.opacity != opacity ||
      old.density != density ||
      old.seed != seed ||
      old.tile != tile;
}

/// Fills its area with the page background ([AppTokens.obsidian] by default)
/// plus a [GrainPainter] behind [child]. The grain sits in its own layer so it
/// is not repainted with the content.
class GrainBackground extends StatelessWidget {
  const GrainBackground({super.key, required this.child, this.color, this.opacity = 0.05});

  final Widget child;

  /// Background colour; defaults to the page background of the theme.
  final Color? color;

  /// Strength of the grain.
  final double opacity;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return ColoredBox(
      color: color ?? tokens.obsidian,
      child: CustomPaint(
        painter: GrainPainter(color: tokens.bone, opacity: opacity),
        isComplex: true,
        child: child,
      ),
    );
  }
}

/// Fill and "brush" border of a [RuneCard]: a rectangle with marked (cut)
/// corners whose edges wobble slightly, with a seeded jitter so every card is
/// stable between frames. A second, fainter stroke gives the hand-inked look.
class RuneBorderPainter extends CustomPainter {
  RuneBorderPainter({
    required this.fill,
    required this.border,
    this.corner = 6,
    this.seed = 1,
    this.jitter = 0.7,
    this.strokeWidth = 1.2,
    this.shadow = true,
  });

  final Color fill;
  final Color border;

  /// Size of the cut corners.
  final double corner;
  final int seed;

  /// Maximum wobble of the edges, in logical pixels.
  final double jitter;
  final double strokeWidth;

  /// Whether to draw a soft drop shadow under the card.
  final bool shadow;

  /// The card outline for [size]: an octagon (rectangle with cut corners)
  /// whose straight edges are split into short segments moved by a seeded
  /// jitter perpendicular to the edge.
  Path outline(Size size, {int salt = 0, double inset = 0}) {
    final random = math.Random(seed * 31 + salt);
    final c = math.min(corner, math.min(size.width, size.height) / 4);
    final l = inset, t = inset, r = size.width - inset, b = size.height - inset;
    final corners = [
      Offset(l + c, t),
      Offset(r - c, t),
      Offset(r, t + c),
      Offset(r, b - c),
      Offset(r - c, b),
      Offset(l + c, b),
      Offset(l, b - c),
      Offset(l, t + c),
    ];
    final path = Path()..moveTo(corners.first.dx, corners.first.dy);
    for (var i = 0; i < corners.length; i++) {
      final from = corners[i];
      final to = corners[(i + 1) % corners.length];
      final delta = to - from;
      final length = delta.distance;
      if (length == 0) continue;
      final normal = Offset(-delta.dy / length, delta.dx / length);
      final steps = math.max(1, (length / 14).floor());
      for (var s = 1; s < steps; s++) {
        final point = from + delta * (s / steps);
        final wobble = (random.nextDouble() * 2 - 1) * jitter;
        path.lineTo(point.dx + normal.dx * wobble, point.dy + normal.dy * wobble);
      }
      path.lineTo(to.dx, to.dy);
    }
    return path..close();
  }

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    final body = outline(size, inset: strokeWidth / 2);
    if (shadow) {
      canvas.drawShadow(body, Colors.black, 2, false);
    }
    canvas.drawPath(body, Paint()..color = fill);
    final stroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeJoin = StrokeJoin.miter
      ..strokeWidth = strokeWidth
      ..color = border;
    canvas.drawPath(body, stroke);
    // Second pass of the "brush": thinner, fainter and slightly off.
    canvas.drawPath(
      outline(size, salt: 1, inset: strokeWidth / 2 + 1.5),
      stroke
        ..strokeWidth = strokeWidth * 0.6
        ..color = border.withValues(alpha: border.a * 0.35),
    );
  }

  @override
  bool shouldRepaint(RuneBorderPainter old) =>
      old.fill != fill ||
      old.border != border ||
      old.corner != corner ||
      old.seed != seed ||
      old.jitter != jitter ||
      old.strokeWidth != strokeWidth ||
      old.shadow != shadow;
}

/// Card of the "carved stone" identity: [AppTokens.stone] fill with a brush
/// border in [AppTokens.oldGold], drawn by [RuneBorderPainter] (no images).
/// [ParchmentCard] and [StoneCard] are styled variants of it.
class RuneCard extends StatelessWidget {
  const RuneCard({
    super.key,
    required this.child,
    this.margin,
    this.padding,
    this.onTap,
    this.color,
    this.borderColor,
    this.seed,
  });

  final Widget child;

  /// Defaults to the card theme's margin.
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  /// Fill; defaults to [AppTokens.stone].
  final Color? color;

  /// Border; defaults to [AppTokens.oldGold] at 55 %.
  final Color? borderColor;

  /// Seed of the border wobble; defaults to one derived from the [key] so
  /// sibling cards do not share the exact same edge.
  final int? seed;

  static const corner = 6.0;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    Widget content = padding == null ? child : Padding(padding: padding!, child: child);
    if (onTap != null) content = InkWell(onTap: onTap, child: content);
    // Ink of the card (and of tiles inside it) is drawn on this transparent
    // Material, above the painted fill.
    content = Material(
      type: MaterialType.transparency,
      clipBehavior: onTap == null ? Clip.none : Clip.antiAlias,
      shape: const BeveledRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(corner))),
      child: content,
    );
    return Padding(
      padding: margin ?? theme.cardTheme.margin ?? const EdgeInsets.all(4),
      child: CustomPaint(
        painter: RuneBorderPainter(
          fill: color ?? tokens.stone,
          border: borderColor ?? tokens.oldGold.withValues(alpha: 0.55),
          corner: corner,
          seed: seed ?? (key?.hashCode ?? 1),
        ),
        child: content,
      ),
    );
  }
}
