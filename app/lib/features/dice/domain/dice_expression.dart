import 'dart:math';

/// Advantage state of a d20 roll.
enum AdvantageMode { normal, advantage, disadvantage }

const _maxDicePerPiece = 100;
const _maxSides = 1000;
const _maxPieces = 20;
const _maxConstant = 100000;

/// One term of a dice expression: a constant (`+3`) or a group of dice
/// (`4d6kh3`, `2d8r2`, `adv`), with the sign it is added with.
class DicePiece {
  const DicePiece.constant(this.constant, {this.sign = 1})
    : count = 0,
      sides = 0,
      keepHighest = null,
      keepLowest = null,
      rerollAtMost = null,
      advantage = AdvantageMode.normal;

  const DicePiece.dice({
    required this.count,
    required this.sides,
    this.sign = 1,
    this.keepHighest,
    this.keepLowest,
    this.rerollAtMost,
    this.advantage = AdvantageMode.normal,
  }) : constant = 0;

  /// `adv` / `dis`: two d20, keeping the highest / lowest one.
  factory DicePiece.advantage(AdvantageMode mode, {int sign = 1}) {
    assert(mode != AdvantageMode.normal);
    final adv = mode == AdvantageMode.advantage;
    return DicePiece.dice(
      count: 2,
      sides: 20,
      sign: sign,
      keepHighest: adv ? 1 : null,
      keepLowest: adv ? null : 1,
      advantage: mode,
    );
  }

  /// 1 or -1.
  final int sign;

  /// Number of dice; 0 for a constant.
  final int count;

  /// Faces of each die; 0 for a constant.
  final int sides;
  final int constant;
  final int? keepHighest;
  final int? keepLowest;

  /// Dice that show this value or less are rolled again, once.
  final int? rerollAtMost;

  /// Set when the piece was written as `adv` / `dis`.
  final AdvantageMode advantage;

  bool get isConstant => sides == 0;

  /// How many dice count towards the total.
  int get keptCount => keepHighest ?? keepLowest ?? count;

  /// A single d20 (plain, `adv` or `dis`): the roll that can be a critical.
  bool get isD20Roll => sides == 20 && keptCount == 1;

  /// Canonical text without the sign: "4d6kh3", "adv", "7".
  String get body {
    if (isConstant) return '$constant';
    if (advantage != AdvantageMode.normal) {
      return advantage == AdvantageMode.advantage ? 'adv' : 'dis';
    }
    final buffer = StringBuffer('${count}d$sides');
    if (keepHighest != null) buffer.write('kh$keepHighest');
    if (keepLowest != null) buffer.write('kl$keepLowest');
    if (rerollAtMost != null) buffer.write('r$rerollAtMost');
    return buffer.toString();
  }

  DicePiece _copyCount(int newCount) => DicePiece.dice(
    count: newCount,
    sides: sides,
    sign: sign,
    keepHighest: keepHighest,
    keepLowest: keepLowest,
    rerollAtMost: rerollAtMost,
  );

  /// The same piece with twice the dice (critical hits). Constants and
  /// advantage rolls are returned unchanged.
  DicePiece doubled() => isConstant || advantage != AdvantageMode.normal
      ? this
      : _copyCount(min(count * 2, _maxDicePerPiece));
}

/// The outcome of one die.
class DieResult {
  const DieResult({required this.sides, required this.value, this.rerolledFrom, this.kept = true});

  final int sides;

  /// The face that counts (after a reroll).
  final int value;

  /// The face discarded by a reroll (`r<n>`), if any.
  final int? rerolledFrom;

  /// False for dice dropped by `kh` / `kl`.
  final bool kept;
}

/// Roll of one [DicePiece] with the dice that were thrown.
class PieceResult {
  const PieceResult({required this.piece, required this.dice, required this.subtotal});

  final DicePiece piece;

  /// Empty for constants.
  final List<DieResult> dice;

  /// Signed contribution to the total.
  final int subtotal;
}

/// Result of rolling a [DiceExpression], with the per-die breakdown.
class DiceResult {
  const DiceResult({required this.expression, required this.pieces, required this.total});

  final DiceExpression expression;
  final List<PieceResult> pieces;
  final int total;

  /// The kept die of the only d20 roll of the expression, or null when there
  /// is no single d20 roll (so no critical or fumble applies).
  int? get d20Value {
    PieceResult? only;
    for (final p in pieces) {
      if (p.piece.isD20Roll) {
        if (only != null) return null;
        only = p;
      }
    }
    if (only == null) return null;
    return only.dice.firstWhere((d) => d.kept).value;
  }

  /// Natural 20 on a single d20 roll.
  bool get isCritical => d20Value == 20;

  /// Natural 1 on a single d20 roll.
  bool get isFumble => d20Value == 1;

  /// "[4 3 (1)] + 2" style text: kept dice plain, dropped ones in parentheses,
  /// rerolled ones as "1>5".
  String get breakdown {
    final buffer = StringBuffer();
    for (var i = 0; i < pieces.length; i++) {
      final p = pieces[i];
      final sign = p.piece.sign < 0 ? '-' : '+';
      if (i == 0) {
        if (p.piece.sign < 0) buffer.write('-');
      } else {
        buffer.write(' $sign ');
      }
      if (p.piece.isConstant) {
        buffer.write(p.piece.constant);
      } else {
        buffer.write('[');
        buffer.write(
          p.dice
              .map((d) {
                final text = d.rerolledFrom == null ? '${d.value}' : '${d.rerolledFrom}>${d.value}';
                return d.kept ? text : '($text)';
              })
              .join(' '),
        );
        buffer.write(']');
      }
    }
    return buffer.toString();
  }
}

/// A parsed dice expression (at least one piece) such as `2d6+3`, `4d6kh3`, `adv+5`, `2d6r2` or
/// `1d8+1d6+3`.
///
/// Syntax: `NdM` (N optional), `kh<n>` / `kl<n>` keep the highest / lowest n,
/// `r<n>` rolls again once the dice that show n or less, `adv` / `dis` equal
/// `2d20kh1` / `2d20kl1` (also as a suffix of a single d20), pieces joined with
/// `+` or `-`, and plain numbers as constants.
class DiceExpression {
  const DiceExpression(this.pieces);

  final List<DicePiece> pieces;

  /// Parses [input]; throws a [FormatException] with a Spanish message when it
  /// is not a valid expression.
  factory DiceExpression.parse(String input) {
    final text = input.toLowerCase().replaceAll(RegExp(r'\s+'), '');
    if (text.isEmpty) {
      throw const FormatException('Escribe una expresión de dados, por ejemplo 1d20+5.');
    }
    final pieces = <DicePiece>[];
    var sign = 1;
    var start = 0;
    for (var i = 0; i <= text.length; i++) {
      final atEnd = i == text.length;
      if (!atEnd && text[i] != '+' && text[i] != '-') continue;
      final term = text.substring(start, i);
      if (term.isEmpty) {
        // A sign before the first term ("-1d4", "+5") is allowed; anywhere else
        // an empty term means two signs in a row or a trailing sign.
        if (i == 0 && !atEnd) {
          sign = text[i] == '-' ? -1 : 1;
          start = i + 1;
          continue;
        }
        throw const FormatException(
          'La expresión está incompleta: falta un valor junto a un signo.',
        );
      }
      pieces.add(_parsePiece(term, sign));
      if (!atEnd) sign = text[i] == '-' ? -1 : 1;
      start = i + 1;
    }
    if (pieces.length > _maxPieces) {
      throw const FormatException('La expresión tiene demasiadas partes (máximo 20).');
    }
    return DiceExpression(pieces);
  }

  /// Like [parse] but returns null when the text is not valid.
  static DiceExpression? tryParse(String input) {
    try {
      return DiceExpression.parse(input);
    } on FormatException {
      return null;
    }
  }

  static DicePiece _parsePiece(String term, int sign) {
    if (RegExp(r'^\d+$').hasMatch(term)) {
      final value = int.tryParse(term);
      if (value == null || value > _maxConstant) {
        throw FormatException('El número "$term" es demasiado grande.');
      }
      return DicePiece.constant(value, sign: sign);
    }
    if (term == 'adv') return DicePiece.advantage(AdvantageMode.advantage, sign: sign);
    if (term == 'dis') return DicePiece.advantage(AdvantageMode.disadvantage, sign: sign);

    final match = RegExp(r'^(\d*)d(\d+)(.*)$').firstMatch(term);
    if (match == null) {
      throw FormatException(
        '"$term" no es una parte válida. Usa NdM, números, kh, kl, r, adv o dis.',
      );
    }
    final count = match.group(1)!.isEmpty ? 1 : int.tryParse(match.group(1)!);
    final sides = int.tryParse(match.group(2)!);
    if (count == null || count < 1 || count > _maxDicePerPiece) {
      throw const FormatException('Puedes tirar entre 1 y 100 dados por parte.');
    }
    if (sides == null || sides < 2 || sides > _maxSides) {
      throw const FormatException('Los dados deben tener entre 2 y 1000 caras.');
    }

    int? keepHighest;
    int? keepLowest;
    int? reroll;
    var advantage = AdvantageMode.normal;
    var rest = match.group(3)!;
    final modifier = RegExp(r'^(kh|kl|r|adv|dis)(\d*)');
    while (rest.isNotEmpty) {
      final m = modifier.firstMatch(rest);
      if (m == null) {
        throw FormatException('"$term" tiene un modificador desconocido: "$rest".');
      }
      final name = m.group(1)!;
      final digits = m.group(2)!;
      rest = rest.substring(m.end);
      if (name == 'adv' || name == 'dis') {
        if (digits.isNotEmpty) {
          throw FormatException('"$name" no admite un número.');
        }
        if (advantage != AdvantageMode.normal) {
          throw const FormatException('No puedes combinar ventaja y desventaja varias veces.');
        }
        advantage = name == 'adv' ? AdvantageMode.advantage : AdvantageMode.disadvantage;
        continue;
      }
      final n = digits.isEmpty ? null : int.tryParse(digits);
      if (n == null) {
        throw FormatException('El modificador "$name" necesita un número, por ejemplo ${name}2.');
      }
      switch (name) {
        case 'kh':
          if (keepHighest != null) throw const FormatException('"kh" aparece dos veces.');
          keepHighest = n;
        case 'kl':
          if (keepLowest != null) throw const FormatException('"kl" aparece dos veces.');
          keepLowest = n;
        default:
          if (reroll != null) throw const FormatException('"r" aparece dos veces.');
          reroll = n;
      }
    }

    if (keepHighest != null && keepLowest != null) {
      throw const FormatException('No puedes combinar "kh" y "kl" en la misma parte.');
    }
    final keep = keepHighest ?? keepLowest;
    if (keep != null && (keep < 1 || keep > count)) {
      throw FormatException('Solo puedes quedarte con entre 1 y $count dados en "$term".');
    }
    if (reroll != null && (reroll < 1 || reroll >= sides)) {
      throw FormatException('"r$reroll" debe estar entre 1 y ${sides - 1} en "$term".');
    }
    if (advantage != AdvantageMode.normal) {
      if (count != 1 || sides != 20 || keep != null || reroll != null) {
        throw const FormatException('Ventaja y desventaja solo se aplican a un d20.');
      }
      return DicePiece.advantage(advantage, sign: sign);
    }
    return DicePiece.dice(
      count: count,
      sides: sides,
      sign: sign,
      keepHighest: keepHighest,
      keepLowest: keepLowest,
      rerollAtMost: reroll,
    );
  }

  /// Canonical text: "2d6+3", "adv-1".
  @override
  String toString() {
    final buffer = StringBuffer();
    for (var i = 0; i < pieces.length; i++) {
      final p = pieces[i];
      if (i == 0) {
        if (p.sign < 0) buffer.write('-');
      } else {
        buffer.write(p.sign < 0 ? '-' : '+');
      }
      buffer.write(p.body);
    }
    return buffer.toString();
  }

  /// True when the expression has a plain `1d20` / `adv` / `dis` roll.
  bool get hasD20Roll => pieces.any((p) => p.isD20Roll);

  /// Rolls every piece. [random] makes the roll deterministic in tests.
  DiceResult roll([Random? random]) {
    final rng = random ?? Random();
    final results = <PieceResult>[];
    var total = 0;
    for (final piece in pieces) {
      if (piece.isConstant) {
        final value = piece.sign * piece.constant;
        results.add(PieceResult(piece: piece, dice: const [], subtotal: value));
        total += value;
        continue;
      }
      final rolled = <({int value, int? from})>[];
      for (var i = 0; i < piece.count; i++) {
        var value = rng.nextInt(piece.sides) + 1;
        int? from;
        final threshold = piece.rerollAtMost;
        if (threshold != null && value <= threshold) {
          from = value;
          value = rng.nextInt(piece.sides) + 1;
        }
        rolled.add((value: value, from: from));
      }
      final kept = List<bool>.filled(rolled.length, true);
      final keepHigh = piece.keepHighest;
      final keepLow = piece.keepLowest;
      if (keepHigh != null || keepLow != null) {
        final order = List<int>.generate(rolled.length, (i) => i)
          ..sort((a, b) {
            final byValue = rolled[a].value.compareTo(rolled[b].value);
            final cmp = keepHigh != null ? -byValue : byValue;
            return cmp != 0 ? cmp : a.compareTo(b);
          });
        final keep = keepHigh ?? keepLow!;
        for (var i = keep; i < order.length; i++) {
          kept[order[i]] = false;
        }
      }
      final dice = [
        for (var i = 0; i < rolled.length; i++)
          DieResult(
            sides: piece.sides,
            value: rolled[i].value,
            rerolledFrom: rolled[i].from,
            kept: kept[i],
          ),
      ];
      final sum = dice.where((d) => d.kept).fold<int>(0, (s, d) => s + d.value);
      results.add(PieceResult(piece: piece, dice: dice, subtotal: piece.sign * sum));
      total += piece.sign * sum;
    }
    return DiceResult(expression: this, pieces: results, total: total);
  }

  /// Critical hit damage: every die of the expression is doubled, constants stay.
  DiceExpression doubleDice() => DiceExpression([for (final p in pieces) p.doubled()]);

  /// Turns the first plain `1d20` into `adv` / `dis`. Returns this expression
  /// when [mode] is normal or there is no plain `1d20` to change.
  DiceExpression withAdvantage(AdvantageMode mode) {
    if (mode == AdvantageMode.normal) return this;
    final index = pieces.indexWhere(
      (p) =>
          p.sides == 20 &&
          p.count == 1 &&
          p.advantage == AdvantageMode.normal &&
          p.rerollAtMost == null,
    );
    if (index < 0) return this;
    return DiceExpression([
      for (var i = 0; i < pieces.length; i++)
        i == index ? DicePiece.advantage(mode, sign: pieces[i].sign) : pieces[i],
    ]);
  }
}

/// "+3", "-1" or "" for zero: the text appended to a dice expression for a bonus.
String bonusSuffix(int bonus) => bonus == 0 ? '' : (bonus > 0 ? '+$bonus' : '$bonus');

/// d20 expression with a modifier and an advantage mode: "1d20+5", "adv+5", "dis-1".
String d20Expression(int bonus, {AdvantageMode mode = AdvantageMode.normal}) {
  final base = switch (mode) {
    AdvantageMode.normal => '1d20',
    AdvantageMode.advantage => 'adv',
    AdvantageMode.disadvantage => 'dis',
  };
  return '$base${bonusSuffix(bonus)}';
}
