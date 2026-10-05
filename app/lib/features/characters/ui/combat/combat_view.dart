import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
/// every control that writes. [header] goes on top (name and view switch).
class CombatView extends ConsumerWidget {
  const CombatView({super.key, required this.character, required this.canEdit, this.header});

  final CharacterDetail character;
  final bool canEdit;
  final Widget? header;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    return ListView(
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
        ResourcesSection(character: c, canEdit: canEdit),
        ClassPanelsSection(character: c, canEdit: canEdit),
        ConsumablesSection(character: c, canEdit: canEdit),
        RestSection(character: c, canEdit: canEdit),
      ],
    );
  }
}
