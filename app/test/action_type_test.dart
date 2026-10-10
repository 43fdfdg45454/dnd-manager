import 'package:opentrpg/core/theme/tokens.dart';
import 'package:opentrpg/core/ui/action_type.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('ActionKind.fromCastingTime', () {
    test('acción, acción adicional y reacción', () {
      expect(ActionKind.fromCastingTime('1 action'), const ActionTiming(ActionKind.action));
      expect(
        ActionKind.fromCastingTime('1 bonus action'),
        const ActionTiming(ActionKind.bonusAction),
      );
      expect(ActionKind.fromCastingTime('1 action').label, 'Acción');
      expect(ActionKind.fromCastingTime('1 bonus action').label, 'Acción adicional');
      expect(ActionKind.fromCastingTime('1 reaction').label, 'Reacción');
      expect(ActionKind.fromCastingTime('1 reaction').note, isNull);
    });

    test('la reacción guarda su condición', () {
      final timing = ActionKind.fromCastingTime(
        '1 reaction, which you take when you are hit by an attack or targeted by the '
        'magic missile spell',
      );
      expect(timing.kind, ActionKind.reaction);
      expect(timing.label, 'Reacción');
      expect(
        timing.note,
        'which you take when you are hit by an attack or targeted by the magic missile spell',
      );
    });

    test('los tiempos largos del SRD se traducen', () {
      for (final (time, label) in [
        ('1 minute', '1 minuto'),
        ('10 minutes', '10 minutos'),
        ('1 hour', '1 hora'),
        ('8 hours', '8 horas'),
        ('24 hours', '24 horas'),
      ]) {
        final timing = ActionKind.fromCastingTime(time);
        expect(timing.kind, ActionKind.other, reason: time);
        expect(timing.label, label, reason: time);
      }
    });

    test('un tiempo desconocido se muestra tal cual', () {
      expect(ActionKind.fromCastingTime('1 round').label, '1 round');
      expect(ActionKind.fromCastingTime(null).kind, ActionKind.other);
    });
  });

  test('los cuatro tipos tienen colores distintos en la paleta por defecto', () {
    for (final tokens in [AppTokens.dark, AppTokens.light]) {
      final colors = {for (final kind in ActionKind.values) kind.color(tokens)};
      expect(colors, hasLength(4));
      final texts = {for (final kind in ActionKind.values) kind.textColor(tokens)};
      expect(texts, hasLength(4));
    }
    expect(ActionKind.action.color(AppTokens.dark), AppTokens.dark.ember);
    expect(ActionKind.bonusAction.color(AppTokens.dark), AppTokens.dark.oldGold);
    expect(ActionKind.reaction.color(AppTokens.dark), AppTokens.dark.arcane);
    expect(ActionKind.other.color(AppTokens.dark), AppTokens.dark.boneMuted);
  });

  testWidgets('el chip muestra el icono y la etiqueta con su clave', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Column(
            children: [
              ActionTypeChip.castingTime('1 bonus action'),
              ActionTypeChip.castingTime('10 minutes', compact: true),
            ],
          ),
        ),
      ),
    );
    expect(find.byKey(const Key('action-kind-bonusAction')), findsOneWidget);
    expect(find.text('Acción adicional'), findsOneWidget);
    expect(find.byKey(const Key('action-kind-other')), findsOneWidget);
    expect(find.text('10 minutos'), findsOneWidget);
  });
}
