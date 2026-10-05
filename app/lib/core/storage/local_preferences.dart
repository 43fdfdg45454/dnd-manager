import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// The loaded `SharedPreferences`, for per-device conveniences (dice history,
/// remembered view modes). Overridden at startup in `main.dart`; null (no
/// persistence, everything lives in memory) when it is not, as in most tests.
final localPreferencesProvider = Provider<SharedPreferences?>((ref) => null);
