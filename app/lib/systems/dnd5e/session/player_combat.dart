import 'package:flutter/material.dart';

import '../../../features/characters/ui/combat/attacks_section.dart';
import '../../../features/characters/ui/combat/class_panels.dart';
import '../../../features/characters/ui/combat/resources_section.dart';
import '../../../features/characters/ui/combat/spells_section.dart';
import '../../../features/characters/ui/combat/vitals_section.dart';
import '../../../features/session/ui/player/combat_items_section.dart';
import '../characters/models.dart';
import 'level_up_card.dart';

/// "Combate" of "Mi sesión": the header of the player, the level granted, hit
/// points, stats, death saves at 0, conditions, attacks, spells, slots,
/// resources, class panels and the items that matter in a fight. The rests
/// live in the "Sesión" sub-tab.
class Dnd5ePlayerCombat extends StatelessWidget {
  const Dnd5ePlayerCombat({super.key, required this.character, this.header});

  final CharacterDetail character;
  final Widget? header;

  @override
  Widget build(BuildContext context) {
    final c = character;
    return ListView(
      key: const Key('player-combat'),
      padding: EdgeInsets.fromLTRB(12, 4, 12, 32 + MediaQuery.paddingOf(context).bottom),
      children: [
        ?header,
        if (c.pendingLevelUpTo != null) LevelUpCard(character: c),
        HpCard(key: const ValueKey('player-hp-card'), character: c, canEdit: true),
        StatsCard(character: c, canEdit: true),
        if (c.hitPointsCurrent == 0) DeathSavesCard(character: c, canEdit: true),
        ConditionsCard(character: c, canEdit: true),
        AttacksSection(character: c),
        SpellsSection(character: c, canEdit: true),
        SpellSlotsSection(character: c, canEdit: true),
        ResourcesSection(character: c, canEdit: true),
        ClassPanelsSection(character: c, canEdit: true),
        CombatItemsSection(character: c, canEdit: true),
      ],
    );
  }
}
