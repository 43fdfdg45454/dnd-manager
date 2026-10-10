import 'dart:math';

import 'package:opentrpg_core/features/characters/domain/measures.dart';
import 'package:opentrpg_core/features/dice/domain/dice_expression.dart';

import '../../catalog/data/models.dart' show HeightWeightTable;

export 'package:opentrpg_core/features/characters/domain/measures.dart';

/// The height and weight tables of the races (PHB chapter 4): rolled with the
/// app's dice engine. The measures themselves are free data of the core
/// (`measures.dart`).

/// Row of the race detail: `base 4' 8" + 2d10; 110 lb × 2d4`.
String describeHeightWeightTable(HeightWeightTable table) =>
    'base ${feetAndInches(table.baseHeightInches)} + ${table.heightModifier}; '
    '${table.baseWeightPounds} lb × ${table.weightModifier}';

/// Outcome of rolling a [HeightWeightTable].
class HeightWeightRoll {
  const HeightWeightRoll({required this.table, required this.heightRoll, required this.weightRoll});

  final HeightWeightTable table;
  final int heightRoll;
  final int weightRoll;

  int get heightInches => table.baseHeightInches + heightRoll;

  /// Base + height roll × weight roll.
  int get weightPounds => table.baseWeightPounds + heightRoll * weightRoll;

  /// `Altura 2d10 = 11 → 5' 7" · Peso 2d4 = 5 → 165 lb`.
  String get summary =>
      'Altura ${table.heightModifier} = $heightRoll → ${feetAndInches(heightInches)} · '
      'Peso ${table.weightModifier} = $weightRoll → $weightPounds lb';
}

/// Rolls both modifiers with [random] (the app's dice engine). Null when a
/// modifier is not a valid dice expression.
HeightWeightRoll? rollHeightWeight(HeightWeightTable table, Random random) {
  final height = DiceExpression.tryParse(table.heightModifier);
  final weight = DiceExpression.tryParse(table.weightModifier);
  if (height == null || weight == null) return null;
  return HeightWeightRoll(
    table: table,
    heightRoll: height.roll(random).total,
    weightRoll: weight.roll(random).total,
  );
}
