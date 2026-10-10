// Calculated values explained point by point (`ValueBreakdownDto`): every
// value with a bonus travels with its breakdown and the UI shows it with one
// tap (`StatValue`). Shared by every game system.

/// Signed modifier: 3 -> "+3", 0 -> "+0", -1 -> "-1".
String formatModifier(int value) => value >= 0 ? '+$value' : '$value';

int? _int(Object? value) {
  if (value is num) return value.toInt();
  if (value is String) return int.tryParse(value);
  return null;
}

String _str(Object? value) => value == null ? '' : (value is String ? value : value.toString());

/// One term of a calculated value (`BreakdownPartDto`). [source] says where it
/// comes from; D&D 5e uses base, race, subrace, ability, proficiency,
/// expertise, class, armor, shield, item, override or feature.
class BreakdownPart {
  const BreakdownPart({required this.source, required this.label, required this.value});

  factory BreakdownPart.fromJson(Map<String, dynamic> json) => BreakdownPart(
    source: _str(json['source']),
    label: _str(json['label']),
    value: _int(json['value']) ?? 0,
  );

  final String source;
  final String label;
  final int value;
}

/// A calculated value explained point by point (`ValueBreakdownDto`): the
/// [parts] add up to [total].
class ValueBreakdown {
  const ValueBreakdown({this.total = 0, this.parts = const []});

  factory ValueBreakdown.fromJson(Object? raw) {
    if (raw is! Map) return const ValueBreakdown();
    final json = Map<String, dynamic>.from(raw);
    final parts = json['parts'];
    return ValueBreakdown(
      total: _int(json['total']) ?? 0,
      parts: parts is List
          ? [
              for (final p in parts)
                if (p is Map) BreakdownPart.fromJson(Map<String, dynamic>.from(p)),
            ]
          : const [],
    );
  }

  /// Null when [raw] is not an object (older servers).
  static ValueBreakdown? maybeFromJson(Object? raw) =>
      raw is Map ? ValueBreakdown.fromJson(raw) : null;

  final int total;
  final List<BreakdownPart> parts;

  /// True when an item or a manual override contributes to the value.
  bool get hasItemOrOverride => parts.any((p) => p.source == 'item' || p.source == 'override');
}
