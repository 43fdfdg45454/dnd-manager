import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/config/app_config.dart';
import 'core/motion/motion_settings.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'core/update/update_ui.dart';
import 'features/settings/data/appearance_controller.dart';

class DndCompanionApp extends ConsumerWidget {
  const DndCompanionApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final appearance = ref.watch(appearanceProvider);
    final reducedMotion = ref.watch(motionSettingsProvider) == MotionPreference.reduced;
    return MaterialApp.router(
      title: AppConfig.appName,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: appearance.themeMode,
      locale: const Locale('es'),
      supportedLocales: const [Locale('es'), Locale('en')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      routerConfig: router,
      builder: (context, child) {
        final media = MediaQuery.of(context);
        final scale = appearance.textSize.scale;
        return MediaQuery(
          data: scale == 1
              ? media
              : media.copyWith(textScaler: TextScaler.linear(media.textScaler.scale(1) * scale)),
          child: MotionScope(
            reduced: reducedMotion,
            child: UpdateGate(
              navigatorKey: router.routerDelegate.navigatorKey,
              child: child ?? const SizedBox.shrink(),
            ),
          ),
        );
      },
    );
  }
}
