import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';

import 'motion_settings.dart';

/// Length of the page transitions.
const pageTransitionDuration = Duration(milliseconds: 220);

/// Vertical distance the incoming page travels, in logical pixels.
const pageTransitionOffset = 12.0;

/// The incoming page fades in while it rises [pageTransitionOffset] px. Under
/// reduced motion the page appears at once.
Widget fadeSlideTransition(BuildContext context, Animation<double> animation, Widget child) {
  if (MotionScope.reducedOf(context)) return child;
  final curved = animation.drive(CurveTween(curve: Curves.easeOutCubic));
  return FadeTransition(
    opacity: curved,
    child: AnimatedBuilder(
      animation: curved,
      child: child,
      builder: (context, child) => Transform.translate(
        offset: Offset(0, pageTransitionOffset * (1 - curved.value)),
        child: child,
      ),
    ),
  );
}

/// A go_router page with the fade + slide transition, for routes declared with
/// `pageBuilder`.
CustomTransitionPage<T> fadeSlidePage<T>({
  required LocalKey key,
  required Widget child,
  String? name,
  Object? arguments,
}) => CustomTransitionPage<T>(
  key: key,
  name: name,
  arguments: arguments,
  child: child,
  transitionDuration: pageTransitionDuration,
  reverseTransitionDuration: pageTransitionDuration,
  transitionsBuilder: (context, animation, secondaryAnimation, child) =>
      fadeSlideTransition(context, animation, child),
);

/// The same transition for every `MaterialPageRoute` (set in the theme's
/// `pageTransitionsTheme`).
class FadeSlidePageTransitionsBuilder extends PageTransitionsBuilder {
  const FadeSlidePageTransitionsBuilder();

  @override
  Duration get transitionDuration => pageTransitionDuration;

  @override
  Widget buildTransitions<T>(
    PageRoute<T> route,
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) => fadeSlideTransition(context, animation, child);
}
