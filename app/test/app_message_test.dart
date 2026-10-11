import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/ui/app_message.dart';
import 'package:opentrpg_core/features/campaigns/ui/feedback.dart';

/// A page with a button that runs [onPressed] from a context under a Scaffold.
Widget _host(void Function(BuildContext context) onPressed) => MaterialApp(
  home: Scaffold(
    body: Builder(
      builder: (context) => TextButton(
        key: const Key('show'),
        onPressed: () => onPressed(context),
        child: const Text('Mostrar'),
      ),
    ),
  ),
);

/// Pumps the time a message stays on screen plus its exit animation.
Future<void> _waitForDismiss(WidgetTester tester) async {
  await tester.pump(appMessageDuration);
  await tester.pumpAndSettle();
}

void main() {
  group('mensajes de la app', () {
    testWidgets('un mensaje desaparece a los tres segundos', (tester) async {
      await tester.pumpWidget(_host((context) => showAppMessage(context, 'Hecho.')));
      await tester.tap(find.byKey(const Key('show')));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 500));
      expect(find.text('Hecho.'), findsOneWidget);
      expect(tester.widget<SnackBar>(find.byType(SnackBar)).duration, appMessageDuration);

      await tester.pump(const Duration(seconds: 2));
      expect(find.text('Hecho.'), findsOneWidget, reason: 'sigue visible antes de los 3 s');

      await _waitForDismiss(tester);
      expect(find.text('Hecho.'), findsNothing);
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('un mensaje nuevo sustituye al anterior y conserva la acción', (tester) async {
      var pressed = 0;
      await tester.pumpWidget(
        _host((context) {
          showAppMessage(context, 'Primero');
          showAppMessage(
            context,
            'Segundo',
            key: const Key('second'),
            action: SnackBarAction(label: 'Deshacer', onPressed: () => pressed++),
          );
        }),
      );
      await tester.tap(find.byKey(const Key('show')));
      await tester.pumpAndSettle();
      expect(find.text('Primero'), findsNothing);
      expect(find.byKey(const Key('second')), findsOneWidget);

      await tester.tap(find.text('Deshacer'));
      await tester.pumpAndSettle();
      expect(pressed, 1);
    });

    testWidgets('con un icono el texto va a su lado', (tester) async {
      await tester.pumpWidget(
        _host(
          (context) => showAppMessage(
            context,
            'Mensaje del DM',
            icon: const Icon(Icons.mail, key: Key('message-icon')),
          ),
        ),
      );
      await tester.tap(find.byKey(const Key('show')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('message-icon')), findsOneWidget);
      expect(find.text('Mensaje del DM'), findsOneWidget);
      await _waitForDismiss(tester);
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('sin ScaffoldMessenger no falla', (tester) async {
      await tester.pumpWidget(
        Directionality(
          textDirection: TextDirection.ltr,
          child: Builder(
            builder: (context) {
              showAppMessage(context, 'Nada');
              return const SizedBox();
            },
          ),
        ),
      );
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('runAction usa el mismo mensaje de tres segundos', (tester) async {
      await tester.pumpWidget(
        _host((context) => runAction(context, () async {}, success: 'Guardado.')),
      );
      await tester.tap(find.byKey(const Key('show')));
      await tester.pumpAndSettle();
      expect(find.text('Guardado.'), findsOneWidget);
      expect(tester.widget<SnackBar>(find.byType(SnackBar)).duration, appMessageDuration);

      await _waitForDismiss(tester);
      expect(find.text('Guardado.'), findsNothing);
    });

    testWidgets('runAction muestra el error con el mismo mensaje', (tester) async {
      await tester.pumpWidget(
        _host((context) => runAction(context, () async => throw StateError('boom'))),
      );
      await tester.tap(find.byKey(const Key('show')));
      await tester.pumpAndSettle();
      expect(find.byType(SnackBar), findsOneWidget);
      expect(tester.widget<SnackBar>(find.byType(SnackBar)).duration, appMessageDuration);
      await _waitForDismiss(tester);
      expect(find.byType(SnackBar), findsNothing);
    });
  });
}
