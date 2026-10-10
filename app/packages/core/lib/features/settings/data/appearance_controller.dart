import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/local_preferences.dart';
import '../../../core/theme/app_style.dart';
import '../../../core/theme/palettes.dart';
import '../../../core/theme/typography.dart';

/// "Tamaño de texto" setting of the Personalización screen.
enum TextSizePreference {
  normal(1),
  large(1.2),
  extraLarge(1.4);

  const TextSizePreference(this.scale);

  /// Factor applied on top of the system text scale.
  final double scale;
}

/// Palette, mode, fonts, text size and rendering switches chosen on the
/// Personalización screen (the animation level lives in
/// `motionSettingsProvider`).
@immutable
class AppearanceSettings {
  const AppearanceSettings({
    this.themeMode = ThemeMode.dark,
    this.textSize = TextSizePreference.normal,
    this.palette = AppPalette.ember,
    this.titleFont = TitleFont.almendra,
    this.bodyFont = BodyFont.sourceSans3,
    this.classColors = true,
    this.textures = true,
  });

  /// Dark by default.
  final ThemeMode themeMode;
  final TextSizePreference textSize;
  final AppPalette palette;
  final TitleFont titleFont;
  final BodyFont bodyFont;

  /// Class accents tint cards and panels.
  final bool classColors;

  /// Stone textures (grain and brush borders).
  final bool textures;

  AppFontSet get fonts => AppFontSet(title: titleFont, body: bodyFont);

  AppStyle get style => AppStyle(classColors: classColors, textures: textures);

  AppearanceSettings copyWith({
    ThemeMode? themeMode,
    TextSizePreference? textSize,
    AppPalette? palette,
    TitleFont? titleFont,
    BodyFont? bodyFont,
    bool? classColors,
    bool? textures,
  }) => AppearanceSettings(
    themeMode: themeMode ?? this.themeMode,
    textSize: textSize ?? this.textSize,
    palette: palette ?? this.palette,
    titleFont: titleFont ?? this.titleFont,
    bodyFont: bodyFont ?? this.bodyFont,
    classColors: classColors ?? this.classColors,
    textures: textures ?? this.textures,
  );

  @override
  bool operator ==(Object other) =>
      other is AppearanceSettings &&
      other.themeMode == themeMode &&
      other.textSize == textSize &&
      other.palette == palette &&
      other.titleFont == titleFont &&
      other.bodyFont == bodyFont &&
      other.classColors == classColors &&
      other.textures == textures;

  @override
  int get hashCode =>
      Object.hash(themeMode, textSize, palette, titleFont, bodyFont, classColors, textures);
}

/// Preference keys of the [AppearanceSettings].
const themeModeKey = 'appearance.theme';
const textSizeKey = 'appearance.textSize';
const paletteKey = 'appearance.palette';
const titleFontKey = 'appearance.titleFont';
const bodyFontKey = 'appearance.bodyFont';
const classColorsKey = 'appearance.classColors';
const texturesKey = 'appearance.textures';

/// Every key of the [AppearanceSettings].
const appearanceKeys = [
  themeModeKey,
  textSizeKey,
  paletteKey,
  titleFontKey,
  bodyFontKey,
  classColorsKey,
  texturesKey,
];

/// The [AppearanceSettings], persisted in `shared_preferences`; the defaults
/// apply when nothing is stored, a stored value is unknown or there is no
/// storage. Every change applies at once.
class AppearanceController extends Notifier<AppearanceSettings> {
  @override
  AppearanceSettings build() {
    final prefs = ref.read(localPreferencesProvider);
    T parse<T extends Enum>(List<T> values, String key, T fallback) {
      final stored = prefs?.getString(key);
      return values.where((value) => value.name == stored).firstOrNull ?? fallback;
    }

    bool flag(String key, bool fallback) {
      final stored = prefs?.get(key);
      return stored is bool ? stored : fallback;
    }

    const defaults = AppearanceSettings();
    return AppearanceSettings(
      themeMode: parse(ThemeMode.values, themeModeKey, defaults.themeMode),
      textSize: parse(TextSizePreference.values, textSizeKey, defaults.textSize),
      palette: parse(AppPalette.values, paletteKey, defaults.palette),
      titleFont: parse(TitleFont.values, titleFontKey, defaults.titleFont),
      bodyFont: parse(BodyFont.values, bodyFontKey, defaults.bodyFont),
      classColors: flag(classColorsKey, defaults.classColors),
      textures: flag(texturesKey, defaults.textures),
    );
  }

  void _store(String key, Object value) {
    final prefs = ref.read(localPreferencesProvider);
    if (prefs == null) return;
    (value is bool ? prefs.setBool(key, value) : prefs.setString(key, '$value')).ignore();
  }

  void selectThemeMode(ThemeMode mode) {
    state = state.copyWith(themeMode: mode);
    _store(themeModeKey, mode.name);
  }

  void selectTextSize(TextSizePreference size) {
    state = state.copyWith(textSize: size);
    _store(textSizeKey, size.name);
  }

  void selectPalette(AppPalette palette) {
    state = state.copyWith(palette: palette);
    _store(paletteKey, palette.name);
  }

  void selectTitleFont(TitleFont font) {
    state = state.copyWith(titleFont: font);
    _store(titleFontKey, font.name);
  }

  void selectBodyFont(BodyFont font) {
    state = state.copyWith(bodyFont: font);
    _store(bodyFontKey, font.name);
  }

  void setClassColors(bool enabled) {
    state = state.copyWith(classColors: enabled);
    _store(classColorsKey, enabled);
  }

  void setTextures(bool enabled) {
    state = state.copyWith(textures: enabled);
    _store(texturesKey, enabled);
  }

  /// Back to the defaults; the stored values are removed.
  void reset() {
    state = const AppearanceSettings();
    final prefs = ref.read(localPreferencesProvider);
    for (final key in appearanceKeys) {
      prefs?.remove(key).ignore();
    }
  }
}

final appearanceProvider = NotifierProvider<AppearanceController, AppearanceSettings>(
  AppearanceController.new,
);
