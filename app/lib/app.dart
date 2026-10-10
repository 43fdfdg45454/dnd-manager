import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/config/app_config.dart';
import 'package:opentrpg_core/core/motion/motion_settings.dart';
import 'package:opentrpg_core/core/router/app_router.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_core/core/update/update_ui.dart';
import 'package:opentrpg_core/features/settings/data/appearance_controller.dart';

class OpenTrpgApp extends ConsumerWidget {
  const OpenTrpgApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final appearance = ref.watch(appearanceProvider);
    final reducedMotion = ref.watch(motionSettingsProvider) == MotionPreference.reduced;
    return MaterialApp.router(
      title: AppConfig.appName,
      theme: AppTheme.light(
        palette: appearance.palette,
        fonts: appearance.fonts,
        style: appearance.style,
      ),
      darkTheme: AppTheme.dark(
        palette: appearance.palette,
        fonts: appearance.fonts,
        style: appearance.style,
      ),
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
