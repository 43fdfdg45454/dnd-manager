import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/local_preferences.dart';

/// "Tamaño de texto" setting of the Personalización screen.
enum TextSizePreference {
  normal(1),
  large(1.2);

  const TextSizePreference(this.scale);

  /// Factor applied on top of the system text scale.
  final double scale;
}

/// Theme and text size chosen on the Personalización screen (the animation
/// level lives in `motionSettingsProvider`).
@immutable
class AppearanceSettings {
  const AppearanceSettings({
    this.themeMode = ThemeMode.dark,
    this.textSize = TextSizePreference.normal,
  });

  /// Dark by default.
  final ThemeMode themeMode;
  final TextSizePreference textSize;

  AppearanceSettings copyWith({ThemeMode? themeMode, TextSizePreference? textSize}) =>
      AppearanceSettings(
        themeMode: themeMode ?? this.themeMode,
        textSize: textSize ?? this.textSize,
      );

  @override
  bool operator ==(Object other) =>
      other is AppearanceSettings && other.themeMode == themeMode && other.textSize == textSize;

  @override
  int get hashCode => Object.hash(themeMode, textSize);
}

/// Preference keys of the [AppearanceSettings].
const themeModeKey = 'appearance.theme';
const textSizeKey = 'appearance.textSize';

/// The [AppearanceSettings], persisted in `shared_preferences`; the defaults
/// apply when nothing is stored or there is no storage.
class AppearanceController extends Notifier<AppearanceSettings> {
  @override
  AppearanceSettings build() {
    final prefs = ref.read(localPreferencesProvider);
    T parse<T extends Enum>(List<T> values, String key, T fallback) {
      final stored = prefs?.getString(key);
      return values.where((value) => value.name == stored).firstOrNull ?? fallback;
    }

    return AppearanceSettings(
      themeMode: parse(ThemeMode.values, themeModeKey, ThemeMode.dark),
      textSize: parse(TextSizePreference.values, textSizeKey, TextSizePreference.normal),
    );
  }

  void selectThemeMode(ThemeMode mode) {
    state = state.copyWith(themeMode: mode);
    ref.read(localPreferencesProvider)?.setString(themeModeKey, mode.name).ignore();
  }

  void selectTextSize(TextSizePreference size) {
    state = state.copyWith(textSize: size);
    ref.read(localPreferencesProvider)?.setString(textSizeKey, size.name).ignore();
  }
}

final appearanceProvider = NotifierProvider<AppearanceController, AppearanceSettings>(
  AppearanceController.new,
);
