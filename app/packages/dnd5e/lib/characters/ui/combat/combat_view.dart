import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/motion/vignette.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';

import '../../../session/level_up_card.dart';
import '../../../session/ui/player/combat_items_section.dart';
import '../../models.dart';
import 'attacks_section.dart';
import 'class_panels.dart';
import 'companion_section.dart';
import 'resources_section.dart';
import 'rest_section.dart';
import 'spells_section.dart';
import 'vitals_section.dart';

/// The tabs of the combat view, in order.
enum CombatSection {
  actions('Acciones'),
  spells('Conjuros'),
  resources('Recursos'),
  more('Más');

  const CombatSection(this.label);

  final String label;
}

/// Preference with the last tab of the combat view of [characterId].
String combatSectionPreference(String characterId) => 'character.$characterId.combatSection';

/// The tabs [character] has something in: "Conjuros" with spells, slots or
/// class rows for it, "Recursos" with limited uses; "Acciones" and "Más"
/// always.
List<CombatSection> combatSectionsOf(CharacterDetail character) => [
  CombatSection.actions,
  if (character.spells.isNotEmpty ||
      regularSlots(character).isNotEmpty ||
      pactSlotsOf(character) != null ||
      hasClassSpellRows(character))
    CombatSection.spells,
  if (hasResources(character)) CombatSection.resources,
  CombatSection.more,
];

/// The combat screen of a character: a status header that never scrolls
/// (hit points with quick damage and healing, the six stats, the death saves
/// at 0 and the concentration and conditions) over four tabs, the last one
/// chosen remembered per character:
///
/// - "Acciones": attacks, the spells it can cast in one tap and what each
///   class does on its turn.
/// - "Conjuros": slots, the spells by level ("Preparados" / "Todos"), rituals
///   and "Preparar conjuros".
/// - "Recursos": every limited use with its pips, recharge and origin.
/// - "Más": companions, consumables, what the classes need to remember,
///   recoveries and rests.
///
/// A tab with nothing to show loses its chip. Every action calls the server
/// and then refreshes the character; nothing changes locally when a request
/// fails.
///
/// [canEdit] false (a player looking at someone else's character) disables
/// every control that writes. [isDm] (a DM or the Owner) rests the character
/// directly; anyone else asks the DM for the rest. [header], when given, goes
/// on top. [inSession] is the player's own view of "Mi sesión": the level
/// granted on top of the tabs, the combat items instead of the quick
/// consumables and no rests (they live in the "Sesión" sub-tab). At 0 hit
/// points a dark vignette closes in on the edges.
class CombatView extends ConsumerStatefulWidget {
  const CombatView({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
    this.header,
    this.inSession = false,
  });

  final CharacterDetail character;
  final bool canEdit;
  final bool isDm;
  final Widget? header;
  final bool inSession;

  @override
  ConsumerState<CombatView> createState() => _CombatViewState();
}

class _CombatViewState extends ConsumerState<CombatView> {
  CombatSection? _section;

  String get _preference => combatSectionPreference(widget.character.id);

  CombatSection? _stored() {
    try {
      final name = ref.read(localPreferencesProvider)?.getString(_preference);
      return CombatSection.values.where((s) => s.name == name).firstOrNull;
    } catch (_) {
      return null;
    }
  }

  void _select(CombatSection section) {
    setState(() => _section = section);
    try {
      ref.read(localPreferencesProvider)?.setString(_preference, section.name);
    } catch (_) {
      // Remembering the tab is a convenience: without storage it is not kept.
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.character;
    final sections = combatSectionsOf(c);
    final wanted = _section ??= _stored();
    final section = sections.contains(wanted) ? wanted! : sections.first;
    final body = Column(
      key: Key(widget.inSession ? 'player-combat' : 'combat-view'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.header case final header?)
          Padding(padding: const EdgeInsets.fromLTRB(12, 4, 12, 0), child: header),
        CombatStatusHeader(character: c, canEdit: widget.canEdit),
        _SectionBar(sections: sections, selected: section, onSelected: _select),
        Expanded(
          child: ListView(
            key: Key('combat-section-${section.name}'),
            padding: EdgeInsets.fromLTRB(16, 0, 16, 96 + MediaQuery.paddingOf(context).bottom),
            children: [
              if (widget.inSession && c.pendingLevelUpTo != null) LevelUpCard(character: c),
              ..._children(section),
            ],
          ),
        ),
      ],
    );
    // "Mi sesión" darkens its own edges.
    return widget.inSession ? body : DarkVignette(active: c.hitPointsCurrent == 0, child: body);
  }

  List<Widget> _children(CombatSection section) {
    final c = widget.character;
    final canEdit = widget.canEdit;
    final isDm = widget.isDm;
    return switch (section) {
      CombatSection.actions => [
        AttacksSection(character: c),
        SpellsSection(character: c, canEdit: canEdit),
        ClassPanelGroups(character: c, part: ClassPanelPart.actions, canEdit: canEdit, isDm: isDm),
      ],
      CombatSection.spells => [
        SpellListSection(
          character: c,
          canEdit: canEdit,
          isDm: isDm,
          spellRows: [
            ClassPanelGroups(
              character: c,
              part: ClassPanelPart.spells,
              canEdit: canEdit,
              isDm: isDm,
            ),
          ],
        ),
      ],
      CombatSection.resources => [ResourcesSection(character: c, canEdit: canEdit, isDm: isDm)],
      CombatSection.more => [
        CompanionSection(character: c, canEdit: canEdit, isDm: isDm),
        if (widget.inSession)
          CombatItemsSection(character: c, canEdit: canEdit)
        else
          ConsumablesSection(character: c, canEdit: canEdit),
        ClassPanelGroups(character: c, part: ClassPanelPart.more, canEdit: canEdit, isDm: isDm),
        if (!widget.inSession) RestSection(character: c, canEdit: canEdit, isDm: isDm),
      ],
    };
  }
}

/// One chip per tab of the combat view, keyed `combat-tab-<section>`.
class _SectionBar extends StatelessWidget {
  const _SectionBar({required this.sections, required this.selected, required this.onSelected});

  final List<CombatSection> sections;
  final CombatSection selected;
  final ValueChanged<CombatSection> onSelected;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Wrap(
        key: const Key('combat-tabs'),
        spacing: 8,
        runSpacing: 4,
        children: [
          for (final section in sections)
            ChoiceChip(
              key: Key('combat-tab-${section.name}'),
              label: Text(section.label),
              selected: section == selected,
              showCheckmark: false,
              onSelected: (_) => onSelected(section),
            ),
        ],
      ),
    );
  }
}
