import 'models.dart';

/// One line of the readable diff of a change request.
typedef PayloadLine = ({String label, String value});

/// One field of an edit: what it was and what it would become.
typedef FieldChange = ({String label, String? before, String after});

/// Readable detail of a change request, shaped by its type. The UI renders
/// each variant differently (a table for sheet edits, a card for an item...).
/// The core declares the variants every game system shares; a game system adds
/// its own (D&D 5e: the sheet edits) through `GameSystemUi.describeChangeRequest`.
abstract class ChangeDetail {
  const ChangeDetail();
}

/// An item to add: its resolved name and what defines it.
final class ItemChangeDetail extends ChangeDetail {
  const ItemChangeDetail({
    required this.name,
    required this.quantity,
    required this.custom,
    required this.lines,
    required this.modifiers,
  });

  final String name;
  final int quantity;

  /// True for a custom item or a catalog item changed by hand.
  final bool custom;

  /// "label: value" facts (category, damage, armor, properties...).
  final List<PayloadLine> lines;

  /// Readable modifiers ("+2 Fuerza", "+1 CA").
  final List<String> modifiers;
}

/// Removing [quantity] of an item; [had] and [left] come from the snapshot.
final class RemoveItemDetail extends ChangeDetail {
  const RemoveItemDetail({required this.name, required this.quantity, this.had});

  final String name;
  final int quantity;
  final int? had;

  int? get left => had == null ? null : had! - quantity;
}

/// Money change with the balance before and after when known.
final class MoneyChangeDetail extends ChangeDetail {
  const MoneyChangeDetail({required this.deltaCp, this.beforeCp, this.reason});

  final int deltaCp;
  final int? beforeCp;
  final String? reason;

  int? get afterCp => beforeCp == null ? null : beforeCp! + deltaCp;
}

/// Nothing to detail (activation, unknown types).
final class PlainChangeDetail extends ChangeDetail {
  const PlainChangeDetail(this.lines);

  final List<PayloadLine> lines;
}

int? _int(Object? value) => value is num ? value.toInt() : int.tryParse('$value');

/// The detail of the request types the core understands by itself (removing
/// an item and money adjustments), or null for the rest, which the game system
/// describes.
ChangeDetail? describeCoreChange(ChangeRequest request) {
  final payload = request.payload;
  final before = request.before;
  return switch (request.type) {
    ChangeRequestType.removeItem => RemoveItemDetail(
      name: '${payload['itemName'] ?? 'Objeto'}',
      quantity: _int(payload['quantity']) ?? 1,
      had: _int(before?['quantity']),
    ),
    ChangeRequestType.adjustMoney => MoneyChangeDetail(
      deltaCp: _int(payload['deltaCp']) ?? 0,
      beforeCp: _int(before?['copperPieces']),
      reason: payload['reason'] is String && '${payload['reason']}'.isNotEmpty
          ? '${payload['reason']}'
          : null,
    ),
    _ => null,
  };
}

/// "key: value" lines of a payload no game system described.
List<PayloadLine> genericPayloadLines(Map<String, dynamic> payload) => [
  for (final e in payload.entries) (label: e.key, value: e.value == null ? '—' : '${e.value}'),
];
