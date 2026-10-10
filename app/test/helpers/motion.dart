import 'package:flutter/widgets.dart';
import 'package:opentrpg_core/core/motion/motion_settings.dart';

/// `MaterialApp.builder` that reduces motion, so the looping animations (the
/// live seal, the flames of a rage, the level-up seal) do not keep
/// `pumpAndSettle` waiting. Tests of the animations themselves pump without it.
Widget reducedMotionBuilder(BuildContext context, Widget? child) =>
    MotionScope(reduced: true, child: child ?? const SizedBox.shrink());
