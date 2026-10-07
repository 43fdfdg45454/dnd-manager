import 'dart:math' as math;

import 'package:flutter/painting.dart';

/// WCAG 2.x relative luminance of [color] (alpha ignored).
double relativeLuminance(Color color) {
  double channel(double c) =>
      c <= 0.03928 ? c / 12.92 : math.pow((c + 0.055) / 1.055, 2.4).toDouble();
  return 0.2126 * channel(color.r) + 0.7152 * channel(color.g) + 0.0722 * channel(color.b);
}

/// WCAG 2.x contrast ratio between [a] and [b], from 1 to 21.
double contrastRatio(Color a, Color b) {
  final la = relativeLuminance(a);
  final lb = relativeLuminance(b);
  return (math.max(la, lb) + 0.05) / (math.min(la, lb) + 0.05);
}

/// The [candidates] colour with the highest contrast on [background].
Color bestOn(Color background, List<Color> candidates) {
  var best = candidates.first;
  for (final c in candidates.skip(1)) {
    if (contrastRatio(c, background) > contrastRatio(best, background)) best = c;
  }
  return best;
}

/// [color] unchanged when it reaches [ratio] on every one of [backgrounds];
/// otherwise moved towards [toward] in small steps until it does. [toward]
/// must itself reach [ratio] on them (it is the text colour of the theme).
Color readableOn(
  Color color,
  List<Color> backgrounds, {
  required Color toward,
  double ratio = 4.5,
}) {
  bool ok(Color c) => backgrounds.every((bg) => contrastRatio(c, bg) >= ratio);
  for (var t = 0.0; t <= 1.0; t += 0.05) {
    final candidate = Color.lerp(color, toward, t)!;
    if (ok(candidate)) return candidate;
  }
  return toward;
}
