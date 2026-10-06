import 'package:flutter/widgets.dart';
import 'package:flutter_svg/flutter_svg.dart';

import 'icons.dart';

/// Renders one of the bundled [AppIcons], tinted with [color] or, when absent,
/// with the colour of the enclosing [IconTheme] (like a Material `Icon`).
/// Falls back to a Material icon if the asset cannot be loaded.
class AppIcon extends StatelessWidget {
  const AppIcon(this.icon, {super.key, this.size = 24, this.color, this.semanticLabel});

  final AppIcons icon;
  final double size;
  final Color? color;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final tint = color ?? IconTheme.of(context).color;
    Widget fallback() => Icon(icon.fallback, size: size, color: tint, semanticLabel: semanticLabel);
    return SvgPicture.asset(
      icon.assetPath,
      width: size,
      height: size,
      colorFilter: tint == null ? null : ColorFilter.mode(tint, BlendMode.srcIn),
      semanticsLabel: semanticLabel,
      excludeFromSemantics: semanticLabel == null,
      placeholderBuilder: (_) => SizedBox.square(dimension: size),
      errorBuilder: (context, error, stackTrace) => fallback(),
    );
  }
}
