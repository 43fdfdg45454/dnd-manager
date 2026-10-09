import 'dart:math';

import 'package:dnd_companion/core/storage/local_preferences.dart';
import 'package:dnd_companion/features/characters/domain/combat_math.dart';
import 'package:dnd_companion/features/dice/data/dice_controller.dart';
import 'package:dnd_companion/features/dice/domain/dice_expression.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Answers the given faces in order (1-based), repeating the last one.
class SequenceRandom implements Random {
  SequenceRandom(this.faces);

  /// Every die shows [face].
  SequenceRandom.always(int face) : faces = [face];

  final List<int> faces;
  int _next = 0;

  @override
  int nextInt(int max) {
    final face = faces[_next < faces.length ? _next++ : faces.length - 1];
    return (face - 1).clamp(0, max - 1);
  }

  @override
  double nextDouble() => 0;

  @override
  bool nextBool() => false;
}

void main() {
  group('DiceExpression.parse', () {
    test('2d6+3 da entre 5 y 15 con un generador fijo y con uno aleatorio', () {
      final fixed = DiceExpression.parse('2d6+3').roll(SequenceRandom.always(4));
      expect(fixed.total, 11);
      expect(fixed.pieces.first.dice.map((d) => d.value), [4, 4]);

      final random = Random(42);
      for (var i = 0; i < 300; i++) {
        final total = DiceExpression.parse('2d6+3').roll(random).total;
        expect(total, inInclusiveRange(5, 15));
      }
    });

    test('4d6kh3 descarta el más bajo y no pasa de 18', () {
      final result = DiceExpression.parse('4d6kh3').roll(SequenceRandom([6, 6, 6, 1]));
      expect(result.total, 18);
      final dice = result.pieces.single.dice;
      expect(dice, hasLength(4));
      expect(dice.where((d) => d.kept), hasLength(3));
      expect(dice.singleWhere((d) => !d.kept).value, 1);

      final random = Random(7);
      for (var i = 0; i < 300; i++) {
        expect(DiceExpression.parse('4d6kh3').roll(random).total, inInclusiveRange(3, 18));
      }
      expect(DiceExpression.parse('4d6kl1').roll(SequenceRandom([5, 2, 6, 3])).total, 2);
    });

    test('adv muestra dos d20 y toma el mayor; dis, el menor', () {
      final adv = DiceExpression.parse('adv').roll(SequenceRandom([5, 17]));
      final dice = adv.pieces.single.dice;
      expect(dice.map((d) => d.value), [5, 17]);
      expect(dice.map((d) => d.sides), [20, 20]);
      expect(adv.total, 17);
      expect(dice.firstWhere((d) => d.kept).value, 17);

      final dis = DiceExpression.parse('dis+2').roll(SequenceRandom([5, 17]));
      expect(dis.total, 7);
      expect(DiceExpression.parse('adv-1').toString(), 'adv-1');
      expect(DiceExpression.parse('1d20adv').toString(), 'adv');
    });

    test('r2 repite una vez los 1 y los 2', () {
      // d1 = 1 -> 5, d2 = 6, d3 = 2 -> 2 (solo se repite una vez).
      final result = DiceExpression.parse('3d6r2').roll(SequenceRandom([1, 5, 6, 2, 2]));
      final dice = result.pieces.single.dice;
      expect(dice.map((d) => d.value), [5, 6, 2]);
      expect(dice.map((d) => d.rerolledFrom), [1, null, 2]);
      expect(result.total, 13);
      expect(result.breakdown, '[1>5 6 2>2]');
    });

    test('varias piezas, signos y desglose', () {
      final expression = DiceExpression.parse(' 1d8 + 1d6 + 3 ');
      expect(expression.toString(), '1d8+1d6+3');
      final result = expression.roll(SequenceRandom([8, 6]));
      expect(result.total, 17);
      expect(result.breakdown, '[8] + [6] + 3');

      final minus = DiceExpression.parse('2d4-1').roll(SequenceRandom.always(1));
      expect(minus.total, 1);
      expect(DiceExpression.parse('-1d4+10').roll(SequenceRandom.always(4)).total, 6);
      expect(DiceExpression.parse('d20').toString(), '1d20');
      expect(DiceExpression.parse('4D6KH3').toString(), '4d6kh3');
    });

    test('una expresión inválida lanza FormatException con un mensaje en español', () {
      final cases = {
        '': 'Escribe una expresión',
        'abc': 'no es una parte válida',
        '2d': 'no es una parte válida',
        'd': 'no es una parte válida',
        '1d0': 'entre 2 y 1000 caras',
        '0d6': 'entre 1 y 100 dados',
        '2d6++3': 'incompleta',
        '2d6+': 'incompleta',
        '4d6kh5': 'entre 1 y 4 dados',
        '4d6kh': 'necesita un número',
        '4d6kh1kl1': 'No puedes combinar',
        '3d6r6': 'debe estar entre 1 y 5',
        '2d6adv': 'solo se aplican a un d20',
        '1d20foo': 'modificador desconocido',
      };
      for (final entry in cases.entries) {
        expect(
          () => DiceExpression.parse(entry.key),
          throwsA(
            isA<FormatException>().having((e) => e.message, 'message', contains(entry.value)),
          ),
          reason: 'expresión "${entry.key}"',
        );
      }
      expect(DiceExpression.tryParse('nope'), isNull);
      expect(DiceExpression.tryParse('1d4'), isNotNull);
    });

    test('crítico y pifia solo con un único d20', () {
      DiceResult roll(String text, List<int> faces) =>
          DiceExpression.parse(text).roll(SequenceRandom(faces));

      expect(roll('1d20+5', [20]).isCritical, isTrue);
      expect(roll('1d20+5', [1]).isFumble, isTrue);
      expect(roll('1d20+5', [10]).isCritical, isFalse);
      // Ventaja: cuenta el dado que se queda.
      expect(roll('adv', [3, 20]).isCritical, isTrue);
      expect(roll('adv', [1, 1]).isFumble, isTrue);
      expect(roll('adv', [1, 12]).isFumble, isFalse);
      expect(roll('dis', [1, 20]).isFumble, isTrue);
      // Bendición: sigue siendo un solo d20.
      expect(roll('1d20+1d4', [20, 2]).isCritical, isTrue);
      // Otros dados o varios d20 no son críticos.
      expect(roll('2d20', [20, 20]).isCritical, isFalse);
      expect(roll('1d20+1d20', [20, 20]).isCritical, isFalse);
      expect(roll('1d12', [12]).isCritical, isFalse);
    });

    test('doubleDice duplica los dados y deja las constantes', () {
      expect(DiceExpression.parse('1d8+3').doubleDice().toString(), '2d8+3');
      expect(DiceExpression.parse('1d8+1d6+2').doubleDice().toString(), '2d8+2d6+2');
      expect(DiceExpression.parse('2d6r2').doubleDice().toString(), '4d6r2');
      expect(DiceExpression.parse('adv').doubleDice().toString(), 'adv');
    });

    test('withAdvantage cambia el primer 1d20 y d20Expression compone ataques', () {
      expect(
        DiceExpression.parse('1d20+5').withAdvantage(AdvantageMode.advantage).toString(),
        'adv+5',
      );
      expect(
        DiceExpression.parse('1d20+1d4').withAdvantage(AdvantageMode.disadvantage).toString(),
        'dis+1d4',
      );
      expect(DiceExpression.parse('2d6').withAdvantage(AdvantageMode.advantage).toString(), '2d6');
      expect(d20Expression(5), '1d20+5');
      expect(d20Expression(-1, mode: AdvantageMode.disadvantage), 'dis-1');
      expect(d20Expression(0, mode: AdvantageMode.advantage), 'adv');
    });
  });

  group('combat_math', () {
    test('el daño consume primero los PG temporales y no baja de 0', () {
      expect(applyDamage(hp: 20, temp: 3, amount: 2), (hp: 20, temp: 1));
      expect(applyDamage(hp: 20, temp: 3, amount: 5), (hp: 18, temp: 0));
      expect(applyDamage(hp: 4, temp: 0, amount: 9), (hp: 0, temp: 0));
    });

    test('la curación no pasa del máximo', () {
      expect(applyHealing(hp: 20, max: 28, amount: 5), 25);
      expect(applyHealing(hp: 20, max: 28, amount: 50), 28);
      expect(applyHealing(hp: 0, max: 0, amount: 3), 3);
    });

    test('una salvación de muerte tirada suma éxito, fallo, dos fallos o 1 PG', () {
      final success = applyDeathSaveRoll(natural: 10, successes: 1, failures: 1);
      expect((success.successes, success.failures, success.revived), (2, 1, false));
      final failure = applyDeathSaveRoll(natural: 9, successes: 1, failures: 1);
      expect((failure.successes, failure.failures), (1, 2));
      final one = applyDeathSaveRoll(natural: 1, successes: 0, failures: 2);
      expect(one.failures, 3);
      final twenty = applyDeathSaveRoll(natural: 20, successes: 2, failures: 2);
      expect((twenty.successes, twenty.failures, twenty.revived), (0, 0, true));
      expect(applyDeathSaveRoll(natural: 15, successes: 3, failures: 0).successes, 3);
    });

    test('las salvaciones de muerte se marcan y se desmarcan', () {
      expect(toggleDeathSave(0, 0), 1);
      expect(toggleDeathSave(1, 2), 3);
      expect(toggleDeathSave(2, 1), 1);
      expect(toggleDeathSave(3, 0), 1);
    });
  });

  group('historial y favoritos', () {
    DiceResult roll(String text) => DiceExpression.parse(text).roll(SequenceRandom.always(3));

    Future<ProviderContainer> open(Map<String, Object> stored, {SharedPreferences? prefs}) async {
      SharedPreferences.setMockInitialValues(stored);
      final container = ProviderContainer(
        overrides: [
          localPreferencesProvider.overrideWithValue(
            prefs ?? await SharedPreferences.getInstance(),
          ),
        ],
      );
      addTearDown(container.dispose);
      return container;
    }

    test('guarda las últimas 100 tiradas y las recupera al reabrir', () async {
      final container = await open({});
      final controller = container.read(diceControllerProvider.notifier);
      for (var i = 0; i < 105; i++) {
        controller.record(roll('1d20+$i'), label: i == 104 ? 'Última' : null);
      }
      controller.toggleFavorite('2d6+3');

      final state = container.read(diceControllerProvider);
      expect(state.history, hasLength(diceHistoryLimit));
      expect(state.history.first.expression, '1d20+104');
      expect(state.history.first.label, 'Última');
      expect(state.history.first.total, 3 + 104);
      expect(state.history.last.expression, '1d20+5');
      expect(state.favorites, ['2d6+3']);

      // Otro arranque con las mismas preferencias.
      final prefs = await SharedPreferences.getInstance();
      final reopened = ProviderContainer(
        overrides: [localPreferencesProvider.overrideWithValue(prefs)],
      );
      addTearDown(reopened.dispose);
      final restored = reopened.read(diceControllerProvider);
      expect(restored.history, hasLength(diceHistoryLimit));
      expect(restored.history.first.label, 'Última');
      expect(restored.favorites, ['2d6+3']);

      reopened.read(diceControllerProvider.notifier).toggleFavorite('2d6+3');
      reopened.read(diceControllerProvider.notifier).clearHistory();
      expect(reopened.read(diceControllerProvider).history, isEmpty);
      expect(reopened.read(diceControllerProvider).favorites, isEmpty);
      expect(prefs.getStringList('dice.history'), isEmpty);
    });

    test('sin almacenamiento el historial vive en memoria', () {
      final container = ProviderContainer();
      addTearDown(container.dispose);
      container.read(diceControllerProvider.notifier).record(roll('1d6'));
      expect(container.read(diceControllerProvider).history, hasLength(1));
    });

    test(
      'el historial guarda el tipo de tirada y solo resalta ataques y salvaciones de muerte',
      () async {
        final container = await open({});
        final controller = container.read(diceControllerProvider.notifier);
        final one = DiceExpression.parse('1d20+5').roll(SequenceRandom.always(1));
        controller.record(one, label: 'Iniciativa');
        controller.record(one, label: 'Ataque: Espada', kind: RollKind.attack);
        final history = container.read(diceControllerProvider).history;
        expect(history[1].kind, RollKind.check);
        expect(history[1].fumble, isTrue);
        expect(history[1].showsFumble, isFalse);
        expect(history[1].natural, 1);
        expect(history[0].kind, RollKind.attack);
        expect(history[0].showsFumble, isTrue);

        final restored = DiceHistoryEntry.tryFromJson(history[0].toJson())!;
        expect(restored.kind, RollKind.attack);
        expect(restored.natural, 1);
      },
    );

    test('las tiradas guardadas sin tipo se deducen de la etiqueta', () async {
      final container = await open({
        'dice.history': [
          '{"expression":"1d20+5","total":6,"label":"Iniciativa","fumble":true}',
          '{"expression":"1d20+5","total":6,"label":"Ataque: Espada","fumble":true}',
          '{"expression":"1d20","total":20,"label":"Salvación de muerte","critical":true}',
        ],
      });
      final history = container.read(diceControllerProvider).history;
      expect(history.map((e) => e.kind), [RollKind.check, RollKind.attack, RollKind.deathSave]);
      expect(history.map((e) => e.showsFumble || e.showsCritical), [false, true, true]);
    });

    test('las líneas corruptas del historial se saltan y el resto se conserva', () async {
      final container = await open({
        'dice.history': ['no es json', '[]', '{"expression":"1d4","total":3,"detail":"[3]"}'],
      });
      expect(container.read(diceControllerProvider).history.map((e) => e.expression), ['1d4']);
    });
  });
}
