// Height and weight of a character (phase 29). Free data without mechanical
// effect: shown in imperial units like the rules, with the metric conversion
// beside them, rounded.

const _cmPerInch = 2.54;
const _kgPerPound = 0.45359237;

/// 67 -> `5' 7" (170 cm)`.
String formatHeight(int inches) => '${feetAndInches(inches)} (${(inches * _cmPerInch).round()} cm)';

/// 165 -> `165 lb (75 kg)`.
String formatWeight(int pounds) => '$pounds lb (${(pounds * _kgPerPound).round()} kg)';

/// 67 -> `5' 7"`; 48 -> `4' 0"`.
String feetAndInches(int inches) => "${inches ~/ 12}' ${inches % 12}\"";

/// `5' 7" (170 cm) · 165 lb (75 kg)`, either part alone, or null when both
/// are missing.
String? formatHeightAndWeight(int? heightInches, int? weightPounds) {
  final parts = [
    if (heightInches != null) formatHeight(heightInches),
    if (weightPounds != null) formatWeight(weightPounds),
  ];
  return parts.isEmpty ? null : parts.join(' · ');
}

/// A height or weight typed by the user: the value when it is a whole number
/// in [min]..[max]; null when empty or invalid (see [measureError]).
int? parseMeasure(String text, {required int min, required int max}) {
  final value = int.tryParse(text.trim());
  return value == null || value < min || value > max ? null : value;
}

/// Spanish error of a typed height or weight, or null when it is valid or
/// empty: "La altura debe ser un número entero entre 1 y 200 pulgadas".
String? measureError(
  String text, {
  required int min,
  required int max,
  required String label,
  required String unit,
}) {
  if (text.trim().isEmpty || parseMeasure(text, min: min, max: max) != null) return null;
  return '$label debe ser un número entero entre $min y $max $unit';
}

/// Limits of the server (`Character.MaxHeightInches` / `MaxWeightPounds`).
const minHeightInches = 1;
const maxHeightInches = 200;
const minWeightPounds = 1;
const maxWeightPounds = 2000;
