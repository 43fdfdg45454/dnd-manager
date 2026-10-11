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

/// What a rolled death saving throw changes (PHB ch. 9): a natural 20 brings
/// the character back with 1 hit point (and clears the saves), a natural 1
/// counts as two failures, 10 or more is a success and less is a failure.
/// Successes never go above 3 and failures above [failuresToDie] (3 unless a
/// feature of a content pack changes it).
({int successes, int failures, bool revived, String message}) applyDeathSaveRoll({
  required int natural,
  required int successes,
  required int failures,
  int failuresToDie = 3,
}) {
  if (natural >= 20) {
    return (successes: 0, failures: 0, revived: true, message: '¡20 natural! Recuperas 1 PG.');
  }
  if (natural <= 1) {
    final next = failures + 2 > failuresToDie ? failuresToDie : failures + 2;
    return (
      successes: successes,
      failures: next,
      revived: false,
      message: next >= failuresToDie
          ? '1 natural: dos fallos. Has muerto.'
          : '1 natural: dos fallos.',
    );
  }
  if (natural >= 10) {
    final next = successes + 1 > 3 ? 3 : successes + 1;
    return (
      successes: next,
      failures: failures,
      revived: false,
      message: next >= 3 ? 'Éxito ($natural): te estabilizas.' : 'Éxito ($natural).',
    );
  }
  final next = failures + 1 > failuresToDie ? failuresToDie : failures + 1;
  return (
    successes: successes,
    failures: next,
    revived: false,
    message: next >= failuresToDie ? 'Fallo ($natural). Has muerto.' : 'Fallo ($natural).',
  );
}
