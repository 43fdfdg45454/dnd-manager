import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/server/server_config_repository.dart';
import 'core/storage/local_preferences.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  runApp(
    ProviderScope(
      overrides: [
        serverConfigRepositoryProvider.overrideWithValue(ServerConfigRepository(prefs)),
        localPreferencesProvider.overrideWithValue(prefs),
      ],
      child: const DndCompanionApp(),
    ),
  );
}
