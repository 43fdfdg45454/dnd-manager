import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/app_icon.dart';
import '../theme/icons.dart';
import '../theme/tokens.dart';
import 'motion_settings.dart';

/// Rune seal that beats (scale and opacity, one cycle every [period]) while
/// [active], e.g. while the live connection is up. Still when inactive or under
/// reduced motion.
///
/// Draws [AppIcons.seal] unless a [child] is given.
class PulseSeal extends StatefulWidget {
  const PulseSeal({
    super.key,
    this.active = true,
    this.child,
    this.size = 24,
    this.color,
    this.semanticLabel,
    this.period = defaultPeriod,
  });

  static const defaultPeriod = Duration(milliseconds: 2400);

  final bool active;

  /// What beats; defaults to the seal icon.
  final Widget? child;

  /// Size of the default seal icon.
  final double size;

  /// Colour of the default seal icon; defaults to [AppTokens.oldGold].
  final Color? color;
  final String? semanticLabel;
  final Duration period;

  @override
  State<PulseSeal> createState() => _PulseSealState();
}

class _PulseSealState extends State<PulseSeal> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: widget.period,
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _sync();
  }

  @override
  void didUpdateWidget(PulseSeal oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.period != widget.period) _controller.duration = widget.period;
    _sync();
  }

  void _sync() {
    final run = widget.active && !MotionScope.reducedOf(context);
    if (run && !_controller.isAnimating) {
      _controller.repeat();
    } else if (!run && _controller.isAnimating) {
      _controller
        ..stop()
        ..value = 0;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final child =
        widget.child ??
        AppIcon(
          AppIcons.seal,
          size: widget.size,
          color: widget.color ?? context.tokens.oldGold,
          semanticLabel: widget.semanticLabel,
        );
    return AnimatedBuilder(
      animation: _controller,
      child: child,
      builder: (context, child) {
        // 0 at the ends of the cycle, 1 in the middle.
        final beat = (1 - math.cos(2 * math.pi * _controller.value)) / 2;
        return Opacity(
          opacity: 1 - 0.35 * beat,
          child: Transform.scale(scale: 1 + 0.12 * beat, child: child),
        );
      },
    );
  }
}
