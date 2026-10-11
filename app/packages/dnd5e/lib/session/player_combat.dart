import 'package:flutter/material.dart';

import '../characters/models.dart';
import '../characters/ui/combat/combat_view.dart';

/// "Combate" of "Mi sesión": the combat view of the player's own character
/// with the level granted on top of the tabs and the items that matter in a
/// fight. The rests live in the "Sesión" sub-tab.
class Dnd5ePlayerCombat extends StatelessWidget {
  const Dnd5ePlayerCombat({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) =>
      CombatView(character: character, canEdit: true, inSession: true);
}
