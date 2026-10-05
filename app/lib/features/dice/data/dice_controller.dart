import 'dart:convert';
import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/storage/local_preferences.dart';
import '../domain/dice_expression.dart';

/// How many rolls the local history keeps.
const diceHistoryLimit = 100;

const _historyKey = 'dice.history';
const _favoritesKey = 'dice.favorites';

/// One past roll, as kept in the local history.
class DiceHistoryEntry {
  const DiceHistoryEntry({
    required this.expression,
    required this.total,
    required this.detail,
    required this.at,
    this.label,
    this.critical = false,
    this.fumble = false,
  });

  factory DiceHistoryEntry.fromResult(DiceResult result, {String? label, DateTime? at}) =>
      DiceHistoryEntry(
        expression: result.expression.toString(),
        total: result.total,
        detail: result.breakdown,
        at: at ?? DateTime.now(),
        label: label,
        critical: result.isCritical,
        fumble: result.isFumble,
      );

  /// Returns null for a malformed entry so a corrupt history never breaks the app.
  static DiceHistoryEntry? tryFromJson(Object? raw) {
    if (raw is! Map) return null;
    final expression = raw['expression'];
    final total = raw['total'];
    if (expression is! String || total is! num) return null;
    final label = raw['label'];
    return DiceHistoryEntry(
      expression: expression,
      total: total.toInt(),
      detail: raw['detail'] is String ? raw['detail'] as String : '',
      at: DateTime.tryParse(raw['at'] is String ? raw['at'] as String : '') ?? DateTime.now(),
      label: label is String && label.isNotEmpty ? label : null,
      critical: raw['critical'] == true,
      fumble: raw['fumble'] == true,
    );
  }

  final String expression;
  final int total;

  /// Per-die breakdown ("[14] + 5").
  final String detail;
  final DateTime at;

  /// What was rolled ("Ataque: Espada larga"), if the roll came from the sheet.
  final String? label;
  final bool critical;
  final bool fumble;

  Map<String, dynamic> toJson() => {
    'expression': expression,
    'total': total,
    'detail': detail,
    'at': at.toIso8601String(),
    'label': ?label,
    if (critical) 'critical': true,
    if (fumble) 'fumble': true,
  };
}

class DiceState {
  const DiceState({this.history = const [], this.favorites = const []});

  /// Newest first, at most [diceHistoryLimit].
  final List<DiceHistoryEntry> history;

  /// Saved expressions, in the order they were starred.
  final List<String> favorites;
}

/// Source of randomness for the rolls; tests override it with a fixed one.
final diceRandomProvider = Provider<Random>((ref) => Random());

/// Local dice history and favorites, persisted in `shared_preferences`.
class DiceController extends Notifier<DiceState> {
  SharedPreferences? get _prefs => ref.read(localPreferencesProvider);

  @override
  DiceState build() {
    final prefs = _prefs;
    if (prefs == null) return const DiceState();
    final history = <DiceHistoryEntry>[];
    for (final line in prefs.getStringList(_historyKey) ?? const <String>[]) {
      // A corrupt line is skipped, the rest of the history stays.
      try {
        final entry = DiceHistoryEntry.tryFromJson(jsonDecode(line));
        if (entry != null) history.add(entry);
      } on FormatException {
        continue;
      }
    }
    return DiceState(
      history: history.take(diceHistoryLimit).toList(),
      favorites: prefs.getStringList(_favoritesKey) ?? const <String>[],
    );
  }

  /// Best effort: a storage failure only loses persistence.
  void _persist() {
    final prefs = _prefs;
    if (prefs == null) return;
    prefs.setStringList(_historyKey, [
      for (final e in state.history) jsonEncode(e.toJson()),
    ]).ignore();
    prefs.setStringList(_favoritesKey, state.favorites).ignore();
  }

  /// Adds a roll at the top of the history.
  void record(DiceResult result, {String? label}) {
    state = DiceState(
      history: [
        DiceHistoryEntry.fromResult(result, label: label),
        ...state.history,
      ].take(diceHistoryLimit).toList(),
      favorites: state.favorites,
    );
    _persist();
  }

  void clearHistory() {
    state = DiceState(favorites: state.favorites);
    _persist();
  }

  void toggleFavorite(String expression) {
    final favorites = [...state.favorites];
    if (!favorites.remove(expression)) favorites.add(expression);
    state = DiceState(history: state.history, favorites: favorites);
    _persist();
  }
}

final diceControllerProvider = NotifierProvider<DiceController, DiceState>(DiceController.new);
