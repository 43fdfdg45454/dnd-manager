import 'package:flutter/widgets.dart';
import 'package:flutter_svg/flutter_svg.dart';

import 'icons.dart';

/// Renders one of the bundled [AppIcons], tinted with [color] or, when absent,
/// with the colour of the enclosing [IconTheme] (like a Material `Icon`).
///
/// Like `Icon`, the size defaults to the enclosing [IconTheme] size (24 when
/// none) and the picture is drawn centered in a square box of exactly that
/// size, so it lines up with text and Material icons in rows, list tiles,
/// chips and buttons. Falls back to a Material icon if the asset cannot be
/// loaded.
class AppIcon extends StatelessWidget {
  const AppIcon(this.icon, {super.key, this.size, this.color, this.semanticLabel});

  final AppIcons icon;

  /// Explicit size; null takes the [IconTheme] size.
  final double? size;
  final Color? color;
  final String? semanticLabel;

  /// Size when neither [size] nor the [IconTheme] gives one.
  static const double defaultSize = 24;

  @override
  Widget build(BuildContext context) {
    final theme = IconTheme.of(context);
    final dimension = size ?? theme.size ?? defaultSize;
    final tint = color ?? theme.color;
    Widget fallback() =>
        Icon(icon.fallback, size: dimension, color: tint, semanticLabel: semanticLabel);
    return SizedBox.square(
      dimension: dimension,
      child: Center(
        child: SvgPicture.asset(
          icon.assetPath,
          width: dimension,
          height: dimension,
          colorFilter: tint == null ? null : ColorFilter.mode(tint, BlendMode.srcIn),
          semanticsLabel: semanticLabel,
          excludeFromSemantics: semanticLabel == null,
          placeholderBuilder: (_) => SizedBox.square(dimension: dimension),
          errorBuilder: (context, error, stackTrace) => fallback(),
        ),
      ),
    );
  }
}
