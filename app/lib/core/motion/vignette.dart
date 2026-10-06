import 'package:flutter/widgets.dart';

import 'motion_settings.dart';

/// Dark vignette over the edges of [child] while [active] (0 PG). Fades in and
/// out in [fadeDuration], or at once under reduced motion. Never takes
/// touches.
class DarkVignette extends StatelessWidget {
  const DarkVignette({
    super.key,
    required this.child,
    required this.active,
    this.color = const Color(0xFF000000),
    this.intensity = 0.75,
  });

  static const fadeDuration = Duration(milliseconds: 400);

  final Widget child;
  final bool active;
  final Color color;

  /// Alpha of [color] at the very edges.
  final double intensity;

  @override
  Widget build(BuildContext context) {
    final reduced = MotionScope.reducedOf(context);
    return Stack(
      children: [
        child,
        Positioned.fill(
          child: IgnorePointer(
            child: AnimatedOpacity(
              key: const Key('dark-vignette'),
              opacity: active ? 1 : 0,
              duration: reduced ? Duration.zero : fadeDuration,
              curve: Curves.easeOut,
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    radius: 1.1,
                    colors: [
                      color.withValues(alpha: 0),
                      color.withValues(alpha: intensity * 0.45),
                      color.withValues(alpha: intensity),
                    ],
                    stops: const [0.5, 0.8, 1],
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
