import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/tokens.dart';
import 'one_shot.dart';

/// Damage feedback: [child] shakes horizontally and is tinted with [color]
/// (default [AppTokens.blood]) for [duration]. Plays when [trigger] changes;
/// see [OneShotMotion].
class ShakeAndTint extends OneShotMotion {
  const ShakeAndTint({
    super.key,
    required this.child,
    super.trigger,
    super.enabled,
    super.playOnMount,
    super.onCompleted,
    this.color,
    this.amplitude = 6,
    this.borderRadius,
  });

  static const duration = Duration(milliseconds: 350);

  final Widget child;
  final Color? color;

  /// Maximum horizontal offset, in logical pixels.
  final double amplitude;

  /// Shape of the tint (e.g. the radius of an HP bar).
  final BorderRadius? borderRadius;

  @override
  State<ShakeAndTint> createState() => _ShakeAndTintState();
}

class _ShakeAndTintState extends OneShotMotionState<ShakeAndTint> {
  @override
  Duration get duration => ShakeAndTint.duration;

  @override
  Widget build(BuildContext context) {
    final color = widget.color ?? context.tokens.blood;
    return AnimatedBuilder(
      animation: controller,
      child: widget.child,
      builder: (context, child) {
        final t = controller.value;
        final live = playing && t > 0 && t < 1;
        final decay = 1 - t;
        // Three full swings that die down.
        final dx = live ? math.sin(t * math.pi * 6) * widget.amplitude * decay : 0.0;
        return Transform.translate(
          offset: Offset(dx, 0),
          child: _Tint(
            color: color.withValues(alpha: live ? 0.45 * decay : 0),
            borderRadius: widget.borderRadius,
            child: child!,
          ),
        );
      },
    );
  }
}

/// Healing feedback: [child] swells slightly and glows with [color] (default
/// [AppTokens.moss]) for [duration]. Plays when [trigger] changes; see
/// [OneShotMotion].
class PulseTint extends OneShotMotion {
  const PulseTint({
    super.key,
    required this.child,
    super.trigger,
    super.enabled,
    super.playOnMount,
    super.onCompleted,
    this.color,
    this.scale = 0.04,
    this.borderRadius,
  });

  static const duration = Duration(milliseconds: 350);

  final Widget child;
  final Color? color;

  /// Extra scale at the peak of the pulse.
  final double scale;
  final BorderRadius? borderRadius;

  @override
  State<PulseTint> createState() => _PulseTintState();
}

class _PulseTintState extends OneShotMotionState<PulseTint> {
  @override
  Duration get duration => PulseTint.duration;

  @override
  Widget build(BuildContext context) {
    final color = widget.color ?? context.tokens.moss;
    return AnimatedBuilder(
      animation: controller,
      child: widget.child,
      builder: (context, child) {
        final t = controller.value;
        final peak = playing && t > 0 && t < 1 ? math.sin(math.pi * t) : 0.0;
        return Transform.scale(
          scale: 1 + widget.scale * peak,
          child: _Tint(
            color: color.withValues(alpha: 0.4 * peak),
            borderRadius: widget.borderRadius,
            child: child!,
          ),
        );
      },
    );
  }
}

/// [child] with a translucent [color] painted over it.
class _Tint extends StatelessWidget {
  const _Tint({required this.color, required this.child, this.borderRadius});

  final Color color;
  final BorderRadius? borderRadius;
  final Widget child;

  @override
  Widget build(BuildContext context) => DecoratedBox(
    position: DecorationPosition.foreground,
    decoration: BoxDecoration(color: color, borderRadius: borderRadius),
    child: child,
  );
}
