import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/motion/vignette.dart';
import '../../data/models.dart';
import 'attacks_section.dart';
import 'class_panels.dart';
import 'resources_section.dart';
import 'rest_section.dart';
import 'vitals_section.dart';

/// The combat screen of a character: one scrolling page with big controls for
/// hit points, conditions, attacks, slots, resources, class panel, consumables
/// and rests. Every action calls the server and then refreshes the character;
/// nothing changes locally when a request fails.
///
/// [canEdit] false (a player looking at someone else's character) disables
/// every control that writes. [isDm] (a DM or the Owner) rests the character
/// directly; anyone else asks the DM for the rest. [header] goes on top (name
/// and view switch). At 0 hit points a dark vignette closes in on the edges.
class CombatView extends ConsumerWidget {
  const CombatView({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
    this.header,
  });

  final CharacterDetail character;
  final bool canEdit;
  final bool isDm;
  final Widget? header;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    return DarkVignette(
      active: c.hitPointsCurrent == 0,
      child: ListView(
        key: const Key('combat-view'),
        padding: EdgeInsets.fromLTRB(16, 8, 16, 96 + MediaQuery.paddingOf(context).bottom),
        children: [
          ?header,
          HpCard(character: c, canEdit: canEdit),
          StatsCard(character: c, canEdit: canEdit),
          if (c.hitPointsCurrent == 0) DeathSavesCard(character: c, canEdit: canEdit),
          ConditionsCard(character: c, canEdit: canEdit),
          AttacksSection(character: c),
          SpellSlotsSection(character: c, canEdit: canEdit),
          ResourcesSection(character: c, canEdit: canEdit, isDm: isDm),
          ClassPanelsSection(character: c, canEdit: canEdit, isDm: isDm),
          ConsumablesSection(character: c, canEdit: canEdit),
          RestSection(character: c, canEdit: canEdit, isDm: isDm),
        ],
      ),
    );
  }
}
