import 'package:flutter/material.dart';

/// Rendering switches of the "Personalización" screen that are not colours:
/// whether class accents tint cards and panels, and whether the stone
/// textures (grain and brush borders) are drawn. Travels in the theme so
/// every widget reads it with `context.appStyle`.
@immutable
class AppStyle extends ThemeExtension<AppStyle> {
  const AppStyle({this.classColors = true, this.textures = true});

  /// Class accents (`ClassAccent`); off uses the palette accent instead.
  final bool classColors;

  /// Grain backgrounds and hand-inked card borders; off draws flat surfaces
  /// with straight edges.
  final bool textures;

  static const standard = AppStyle();

  @override
  AppStyle copyWith({bool? classColors, bool? textures}) =>
      AppStyle(classColors: classColors ?? this.classColors, textures: textures ?? this.textures);

  @override
  AppStyle lerp(ThemeExtension<AppStyle>? other, double t) {
    if (other is! AppStyle) return this;
    return t < 0.5 ? this : other;
  }

  @override
  bool operator ==(Object other) =>
      other is AppStyle && other.classColors == classColors && other.textures == textures;

  @override
  int get hashCode => Object.hash(classColors, textures);
}

extension AppStyleContext on BuildContext {
  /// The [AppStyle] of the closest theme; [AppStyle.standard] when the theme
  /// does not carry one.
  AppStyle get appStyle => Theme.of(this).extension<AppStyle>() ?? AppStyle.standard;
}
