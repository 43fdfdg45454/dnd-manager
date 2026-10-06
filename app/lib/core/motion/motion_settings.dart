import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/local_preferences.dart';

/// "Animaciones" setting of the Personalización screen.
enum MotionPreference {
  /// Every animation plays ("Todas").
  all,

  /// Animations jump to their end state ("Reducidas").
  reduced,
}

/// Preference key of the [MotionPreference].
const motionPreferenceKey = 'appearance.motion';

/// The chosen [MotionPreference], persisted in `shared_preferences`. Defaults
/// to [MotionPreference.all] when nothing is stored or there is no storage.
class MotionSettingsController extends Notifier<MotionPreference> {
  @override
  MotionPreference build() {
    final stored = ref.read(localPreferencesProvider)?.getString(motionPreferenceKey);
    return stored == MotionPreference.reduced.name
        ? MotionPreference.reduced
        : MotionPreference.all;
  }

  void select(MotionPreference preference) {
    state = preference;
    ref.read(localPreferencesProvider)?.setString(motionPreferenceKey, preference.name).ignore();
  }
}

final motionSettingsProvider = NotifierProvider<MotionSettingsController, MotionPreference>(
  MotionSettingsController.new,
);

/// Tells the motion widgets below whether animations are reduced. Placed by
/// the app above the navigator from [motionSettingsProvider]; the system
/// "remove animations" setting ([MediaQueryData.disableAnimations]) reduces
/// them too.
class MotionScope extends InheritedWidget {
  const MotionScope({super.key, required this.reduced, required super.child});

  /// Whether the user chose "animaciones reducidas".
  final bool reduced;

  /// Whether animations must jump to their end state at [context].
  static bool reducedOf(BuildContext context) {
    final scope = context.dependOnInheritedWidgetOfExactType<MotionScope>();
    return (scope?.reduced ?? false) || (MediaQuery.maybeDisableAnimationsOf(context) ?? false);
  }

  @override
  bool updateShouldNotify(MotionScope oldWidget) => reduced != oldWidget.reduced;
}
