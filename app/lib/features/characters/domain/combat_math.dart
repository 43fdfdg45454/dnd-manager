/// Hit points after taking [amount] damage: temporary hit points absorb it
/// first, the rest comes off the current hit points (never below 0).
({int hp, int temp}) applyDamage({required int hp, required int temp, required int amount}) {
  final absorbed = amount < temp ? amount : temp;
  final rest = amount - absorbed;
  final next = hp - rest;
  return (hp: next < 0 ? 0 : next, temp: temp - absorbed);
}

/// Current hit points after healing [amount], capped at [max]. A [max] of 0 or
/// less (not computed yet) means no cap.
int applyHealing({required int hp, required int max, required int amount}) {
  final next = hp + amount;
  return max > 0 && next > max ? max : next;
}

/// Death save count after tapping the circle at [index] (0-based): tapping the
/// last marked circle clears it, any other circle marks up to itself.
int toggleDeathSave(int current, int index) => index + 1 == current ? index : index + 1;
