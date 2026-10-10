/// Coins shown for money, largest first (electrum is left out on purpose).
const _moneyCoins = <(String, int)>[('pp', 1000), ('gp', 100), ('sp', 10), ('cp', 1)];

/// Money in copper as "1 pp 5 gp 5 sp"; "0 cp" for zero and a leading "-" for
/// negative amounts.
String formatMoney(int copper) {
  if (copper == 0) return '0 cp';
  var rest = copper.abs();
  final parts = <String>[];
  for (final (name, value) in _moneyCoins) {
    final amount = rest ~/ value;
    if (amount > 0) parts.add('$amount $name');
    rest -= amount * value;
  }
  return '${copper < 0 ? '-' : ''}${parts.join(' ')}';
}

/// Parses a gold amount ("15", "15.5", "15,5", "-2") into copper; null when it
/// is not a number, is not finite or (unless [allowNegative]) is negative.
int? parseGoldToCp(String text, {bool allowNegative = false}) {
  final value = double.tryParse(text.trim().replaceAll(',', '.'));
  if (value == null || !value.isFinite) return null;
  if (value < 0 && !allowNegative) return null;
  return (value * 100).round();
}
