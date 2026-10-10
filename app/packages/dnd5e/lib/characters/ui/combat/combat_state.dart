import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../models.dart';

/// Rounds a rage lasts (one minute).
const rageRounds = 10;

/// Local (not stored) state of a barbarian's rage.
class RageState {
  const RageState({this.active = false, this.roundsLeft = 0});

  final bool active;
  final int roundsLeft;
}

class RageController extends Notifier<RageState> {
  RageController(this.characterId);

  final String characterId;

  @override
  RageState build() => const RageState();

  void start() => state = const RageState(active: true, roundsLeft: rageRounds);

  /// One round passes; the rage ends when none are left.
  void nextRound() {
    final left = state.roundsLeft - 1;
    state = left <= 0 ? const RageState() : RageState(active: true, roundsLeft: left);
  }

  void end() => state = const RageState();
}

/// Kept for the whole session so the rage survives switching views.
final rageControllerProvider = NotifierProvider.family<RageController, RageState, String>(
  RageController.new,
);

/// The rage damage bonus of the barbarian panel (0 for other characters).
int rageDamageBonusOf(CharacterDetail character) {
  final value = character.combat.panelOf('barbarian')?.data['rageDamageBonus'];
  return value is num ? value.toInt() : 0;
}

/// Local (not stored) flag of a druid in Wild Shape, per character.
class WildShapeController extends Notifier<bool> {
  WildShapeController(this.characterId);

  final String characterId;

  @override
  bool build() => false;

  void set(bool value) => state = value;
}

/// Kept for the whole session so the form survives switching views.
final wildShapeControllerProvider = NotifierProvider.family<WildShapeController, bool, String>(
  WildShapeController.new,
);
