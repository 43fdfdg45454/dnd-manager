import 'package:opentrpg/core/motion/flames.dart';
import 'package:opentrpg/core/motion/flash.dart';
import 'package:opentrpg/core/motion/level_up_celebration.dart';
import 'package:opentrpg/core/motion/motion_settings.dart';
import 'package:opentrpg/core/motion/page_transitions.dart';
import 'package:opentrpg/core/motion/pulse.dart';
import 'package:opentrpg/core/motion/rest_animations.dart';
import 'package:opentrpg/core/motion/shake.dart';
import 'package:opentrpg/core/motion/vignette.dart';
import 'package:opentrpg/core/motion/wax_seal.dart';
import 'package:opentrpg/core/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// [builder] inside the dark theme and a [MotionScope], rebuilt whenever
/// [value] changes.
Future<void> _pump<T>(
  WidgetTester tester,
  ValueNotifier<T> value,
  Widget Function(T value) builder, {
  required bool reduced,
  bool fullScreen = false,
}) async {
  final content = ValueListenableBuilder<T>(
    valueListenable: value,
    builder: (context, v, _) => builder(v),
  );
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.dark(),
      home: MotionScope(
        reduced: reduced,
        child: Scaffold(
          body: fullScreen
              ? content
              : Center(child: SizedBox(width: 300, height: 200, child: content)),
        ),
      ),
    ),
  );
}

const _box = ColoredBox(
  color: Colors.blue,
  child: SizedBox.expand(key: Key('content')),
);

/// Painter of type [P] inside [of].
P _painter<P extends CustomPainter>(WidgetTester tester, Type of, {bool foreground = false}) {
  final paints = tester.widgetList<CustomPaint>(
    find.descendant(of: find.byType(of), matching: find.byType(CustomPaint)),
  );
  return paints.map((p) => foreground ? p.foregroundPainter : p.painter).whereType<P>().first;
}

double _translation(WidgetTester tester, Type of) {
  final transform = tester.widget<Transform>(
    find.descendant(of: find.byType(of), matching: find.byType(Transform)).first,
  );
  return transform.transform.getTranslation().x;
}

double _scale(WidgetTester tester, Type of) {
  final transform = tester.widget<Transform>(
    find.descendant(of: find.byType(of), matching: find.byType(Transform)).first,
  );
  return transform.transform.getMaxScaleOnAxis();
}

void main() {
  test('MotionScope.reducedOf combina el ajuste y el sistema', () {
    expect(const MotionScope(reduced: true, child: SizedBox()).reduced, isTrue);
  });

  for (final reduced in [false, true]) {
    final mode = reduced ? 'reducidas' : 'todas';

    group('animaciones $mode', () {
      testWidgets('PulseSeal late solo mientras está activo', (tester) async {
        final active = ValueNotifier(true);
        await _pump(tester, active, (a) => PulseSeal(active: a), reduced: reduced);
        await tester.pump(const Duration(milliseconds: 1200));

        expect(tester.takeException(), isNull);
        expect(tester.hasRunningAnimations, !reduced);
        expect(_scale(tester, PulseSeal), reduced ? 1 : closeTo(1.12, 0.01));

        active.value = false;
        await tester.pump();
        await tester.pump(PulseSeal.defaultPeriod);
        expect(tester.hasRunningAnimations, isFalse);
        expect(_scale(tester, PulseSeal), 1);
      });

      testWidgets('FlameBorder enciende, mantiene la brasa y se apaga', (tester) async {
        final active = ValueNotifier(false);
        await _pump(
          tester,
          active,
          (a) => FlameBorder(active: a, child: _box),
          reduced: reduced,
        );
        FlamePainter painter() => _painter<FlamePainter>(tester, FlameBorder, foreground: true);
        expect(painter().ignite.value, 0);

        active.value = true;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));
        expect(tester.takeException(), isNull);
        if (reduced) {
          expect(painter().ignite.value, 1);
          expect(tester.hasRunningAnimations, isFalse);
        } else {
          expect(painter().ignite.value, inExclusiveRange(0, 1));
        }

        await tester.pump(FlameBorder.igniteDuration);
        expect(painter().ignite.value, 1);
        // The embers keep moving while active (not under reduced motion).
        expect(tester.hasRunningAnimations, !reduced);

        active.value = false;
        await tester.pump();
        await tester.pump(FlameBorder.igniteDuration);
        expect(painter().ignite.value, 0);
        expect(tester.hasRunningAnimations, isFalse);
        expect(tester.takeException(), isNull);
      });

      testWidgets('FlameBorder no bloquea los toques', (tester) async {
        var taps = 0;
        await _pump(
          tester,
          ValueNotifier(true),
          (_) => FlameBorder(
            active: true,
            child: GestureDetector(onTap: () => taps++, child: _box),
          ),
          reduced: reduced,
        );
        await tester.pump(const Duration(milliseconds: 100));
        await tester.tap(find.byKey(const Key('content')));
        expect(taps, 1);
      });

      testWidgets('RadialFlash destella al dispararse y termina', (tester) async {
        final trigger = ValueNotifier(0);
        var completed = 0;
        await _pump(
          tester,
          trigger,
          (t) => RadialFlash(trigger: t, onCompleted: () => completed++, child: _box),
          reduced: reduced,
        );
        trigger.value++;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 200));
        final progress = _painter<RadialFlashPainter>(tester, RadialFlash).progress.value;
        expect(progress, reduced ? 1 : inExclusiveRange(0, 1));

        await tester.pump(RadialFlash.duration);
        await tester.pump();
        expect(tester.takeException(), isNull);
        expect(tester.hasRunningAnimations, isFalse);
        expect(completed, 1);
      });

      testWidgets('ShakeAndTint sacude y vuelve a su sitio', (tester) async {
        final trigger = ValueNotifier(0);
        var completed = 0;
        await _pump(
          tester,
          trigger,
          (t) => ShakeAndTint(trigger: t, onCompleted: () => completed++, child: _box),
          reduced: reduced,
        );
        trigger.value++;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 100));
        final dx = _translation(tester, ShakeAndTint);
        expect(dx, reduced ? 0 : isNot(0));

        await tester.pump(ShakeAndTint.duration);
        await tester.pump();
        expect(_translation(tester, ShakeAndTint), 0);
        expect(tester.takeException(), isNull);
        expect(completed, 1);
      });

      testWidgets('PulseTint late una vez', (tester) async {
        final trigger = ValueNotifier(0);
        var completed = 0;
        await _pump(
          tester,
          trigger,
          (t) => PulseTint(trigger: t, onCompleted: () => completed++, child: _box),
          reduced: reduced,
        );
        trigger.value++;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 175));
        expect(_scale(tester, PulseTint), reduced ? 1 : greaterThan(1));

        await tester.pump(PulseTint.duration);
        await tester.pump();
        expect(_scale(tester, PulseTint), 1);
        expect(tester.takeException(), isNull);
        expect(completed, 1);
      });

      testWidgets('DarkVignette oscurece mientras está activa', (tester) async {
        final active = ValueNotifier(false);
        await _pump(
          tester,
          active,
          (a) => DarkVignette(active: a, child: _box),
          reduced: reduced,
        );
        AnimatedOpacity opacity() =>
            tester.widget<AnimatedOpacity>(find.byKey(const Key('dark-vignette')));
        expect(opacity().opacity, 0);
        expect(opacity().duration, reduced ? Duration.zero : DarkVignette.fadeDuration);

        active.value = true;
        await tester.pump();
        await tester.pump(DarkVignette.fadeDuration);
        await tester.pump(DarkVignette.fadeDuration);
        expect(opacity().opacity, 1);
        expect(tester.hasRunningAnimations, isFalse);
        expect(tester.takeException(), isNull);
      });

      for (final (name, build, duration) in [
        (
          'CampfireBurst',
          (VoidCallback done) => CampfireBurst(playOnMount: true, onCompleted: done, child: _box),
          CampfireBurst.duration,
        ),
        (
          'MoonPass',
          (VoidCallback done) => MoonPass(playOnMount: true, onCompleted: done, child: _box),
          MoonPass.duration,
        ),
      ]) {
        testWidgets('$name se reproduce y termina', (tester) async {
          var completed = 0;
          await _pump(tester, ValueNotifier(0), (_) => build(() => completed++), reduced: reduced);
          await tester.pump(duration ~/ 2);
          expect(tester.hasRunningAnimations, !reduced);
          expect(tester.takeException(), isNull);

          await tester.pump(duration);
          await tester.pump();
          expect(tester.hasRunningAnimations, isFalse);
          expect(tester.takeException(), isNull);
          expect(completed, 1);
        });
      }

      testWidgets('LevelUpCelebration crece con rebote y se cierra', (tester) async {
        var dismissed = 0;
        await _pump(
          tester,
          ValueNotifier(0),
          (_) => LevelUpCelebration(level: 5, onDismiss: () => dismissed++),
          reduced: reduced,
          fullScreen: true,
        );
        await tester.pump(const Duration(milliseconds: 600));
        expect(tester.hasRunningAnimations, !reduced);
        expect(find.byKey(const Key('level-up-number')), findsOneWidget);
        expect(find.text('5'), findsOneWidget);

        await tester.pump(LevelUpCelebration.duration);
        expect(tester.hasRunningAnimations, isFalse);
        final number = tester.widget<Transform>(
          find
              .ancestor(
                of: find.byKey(const Key('level-up-number')),
                matching: find.byType(Transform),
              )
              .first,
        );
        expect(number.transform.getMaxScaleOnAxis(), closeTo(1, 0.001));
        expect(tester.takeException(), isNull);

        await tester.tap(find.byKey(const Key('level-up-dismiss')));
        expect(dismissed, 1);
      });

      testWidgets('showLevelUpCelebration abre y cierra la superposición', (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.dark(),
            home: MotionScope(
              reduced: reduced,
              child: Builder(
                builder: (context) => Scaffold(
                  body: TextButton(
                    onPressed: () => showLevelUpCelebration(context, level: 3),
                    child: const Text('subir'),
                  ),
                ),
              ),
            ),
          ),
        );
        await tester.tap(find.text('subir'));
        await tester.pump();
        await tester.pump(LevelUpCelebration.duration);
        await tester.pump(const Duration(milliseconds: 300));
        expect(find.byKey(const Key('level-up-celebration')), findsOneWidget);

        await tester.tap(find.byKey(const Key('level-up-dismiss')));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('level-up-celebration')), findsNothing);
      });

      testWidgets('SealBreak rompe el sello y desaparece', (tester) async {
        final broken = ValueNotifier(false);
        var done = 0;
        await _pump(
          tester,
          broken,
          (b) => Center(
            child: SealBreak(broken: b, onBroken: () => done++),
          ),
          reduced: reduced,
        );
        WaxSealPainter painter() => _painter<WaxSealPainter>(tester, SealBreak);
        expect(painter().progress.value, 0);

        broken.value = true;
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));
        expect(painter().progress.value, reduced ? 1 : inExclusiveRange(0, 1));

        await tester.pump(SealBreak.duration);
        await tester.pump();
        expect(painter().progress.value, 1);
        expect(tester.hasRunningAnimations, isFalse);
        expect(tester.takeException(), isNull);
        expect(done, 1);
        expect(tester.getSize(find.byType(SealBreak)), const Size(40, 40));
      });

      testWidgets('fadeSlidePage desvanece y desplaza 12 px', (tester) async {
        final router = GoRouter(
          routes: [
            GoRoute(path: '/', builder: (context, state) => const Text('inicio')),
            GoRoute(
              path: '/b',
              pageBuilder: (context, state) =>
                  fadeSlidePage(key: state.pageKey, child: const Text('destino')),
            ),
          ],
        );
        addTearDown(router.dispose);
        await tester.pumpWidget(
          MaterialApp.router(
            theme: AppTheme.dark(),
            routerConfig: router,
            builder: (context, child) => MotionScope(reduced: reduced, child: child!),
          ),
        );
        router.push('/b');
        await tester.pump();
        await tester.pump(pageTransitionDuration ~/ 2);
        final fades = find.ancestor(
          of: find.text('destino'),
          matching: find.byType(FadeTransition),
        );
        if (reduced) {
          expect(
            find.ancestor(
              of: find.text('destino'),
              matching: find.byWidgetPredicate(
                (w) => w is Transform && w.transform.getTranslation().y != 0,
              ),
            ),
            findsNothing,
          );
        } else {
          final opacity = tester.widget<FadeTransition>(fades.first).opacity.value;
          expect(opacity, inExclusiveRange(0, 1));
        }

        await tester.pumpAndSettle();
        expect(find.text('destino'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });

      testWidgets('el tema usa la transición en las rutas Material', (tester) async {
        final navigator = GlobalKey<NavigatorState>();
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.dark(),
            navigatorKey: navigator,
            builder: (context, child) => MotionScope(reduced: reduced, child: child!),
            home: const Text('inicio'),
          ),
        );
        final route = MaterialPageRoute<void>(builder: (_) => const Text('destino'));
        navigator.currentState!.push(route);
        await tester.pump();
        expect(route.transitionDuration, pageTransitionDuration);
        await tester.pump(pageTransitionDuration ~/ 2);
        expect(
          find.ancestor(of: find.text('destino'), matching: find.byType(FadeTransition)),
          reduced ? findsNothing : findsWidgets,
        );
        await tester.pumpAndSettle();
        expect(find.text('destino'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    });
  }

  testWidgets('enabled: false salta al final aunque las animaciones estén activas', (tester) async {
    final trigger = ValueNotifier(0);
    var completed = 0;
    await _pump(
      tester,
      trigger,
      (t) => ShakeAndTint(trigger: t, enabled: false, onCompleted: () => completed++, child: _box),
      reduced: false,
    );
    trigger.value++;
    await tester.pump();
    await tester.pump();
    expect(tester.hasRunningAnimations, isFalse);
    expect(completed, 1);
  });

  testWidgets('el sistema sin animaciones también las reduce', (tester) async {
    late bool reduced;
    await tester.pumpWidget(
      MediaQuery(
        data: const MediaQueryData(disableAnimations: true),
        child: Builder(
          builder: (context) {
            reduced = MotionScope.reducedOf(context);
            return const SizedBox();
          },
        ),
      ),
    );
    expect(reduced, isTrue);
  });
}
