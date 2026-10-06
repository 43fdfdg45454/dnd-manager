import 'package:flutter/material.dart';

import '../../../../core/theme/components.dart';
import '../../data/models.dart';
import '../../domain/class_theme.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_support.dart';
import 'panels/barbarian.dart';
import 'panels/bard.dart';
import 'panels/cleric.dart';
import 'panels/druid.dart';
import 'panels/fighter.dart';
import 'panels/monk.dart';
import 'panels/paladin.dart';
import 'panels/panel_support.dart';
import 'panels/ranger.dart';
import 'panels/rogue.dart';
import 'panels/sorcerer.dart';
import 'panels/warlock.dart';
import 'panels/wizard.dart';

export 'panels/barbarian.dart';
export 'panels/bard.dart';
export 'panels/cleric.dart';
export 'panels/druid.dart';
export 'panels/fighter.dart';
export 'panels/monk.dart';
export 'panels/paladin.dart';
export 'panels/panel_support.dart';
export 'panels/ranger.dart';
export 'panels/rogue.dart';
export 'panels/sorcerer.dart';
export 'panels/warlock.dart';
export 'panels/wizard.dart';

/// Class panels by `classIndex`. A class without an entry gets
/// [buildGenericPanel]. Register new classes here.
final Map<String, ClassPanelBuilder> classPanelBuilders = {
  'barbarian': (context, panel) => BarbarianPanel(panel: panel),
  'bard': (context, panel) => BardPanel(panel: panel),
  'cleric': (context, panel) => ClericPanel(panel: panel),
  'druid': (context, panel) => DruidPanel(panel: panel),
  'fighter': (context, panel) => FighterPanel(panel: panel),
  'monk': (context, panel) => MonkPanel(panel: panel),
  'paladin': (context, panel) => PaladinPanel(panel: panel),
  'ranger': (context, panel) => RangerPanel(panel: panel),
  'rogue': (context, panel) => RoguePanel(panel: panel),
  'sorcerer': (context, panel) => SorcererPanel(panel: panel),
  'warlock': (context, panel) => WarlockPanel(panel: panel),
  'wizard': (context, panel) => WizardPanel(panel: panel),
};

/// Classes whose panel is built from the server's `ClassPanelDto`: without it
/// they show nothing. The rest work from the class level and the resources.
const serverPanelClasses = {'barbarian', 'wizard', 'paladin'};

/// The panels to show, in class order: the server's panel of each class, or
/// an empty one (class level only) for classes that do not need its data.
/// Server panels of classes the character no longer has go last.
List<ClassPanel> classPanelsOf(CharacterDetail character) {
  final classes = {for (final c in character.classes) c.classIndex};
  return [
    for (final c in character.classes)
      if (character.combat.panelOf(c.classIndex) case final panel?)
        panel
      else if (!serverPanelClasses.contains(c.classIndex) &&
          classPanelBuilders.containsKey(c.classIndex))
        ClassPanel(classIndex: c.classIndex, level: c.level),
    for (final panel in character.combat.classPanels)
      if (!classes.contains(panel.classIndex)) panel,
  ];
}

/// The panels of the character's classes, each with the registered builder
/// and the accent of its class.
class ClassPanelsSection extends StatelessWidget {
  const ClassPanelsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context) {
    final panels = [
      for (final panel in classPanelsOf(character))
        ClassAccent(
          classIndex: panel.classIndex,
          child: Builder(
            builder: (context) => (classPanelBuilders[panel.classIndex] ?? buildGenericPanel)(
              context,
              ClassPanelContext(character: character, panel: panel, canEdit: canEdit),
            ),
          ),
        ),
    ];
    if (panels.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: panels);
  }
}

String _className(CharacterDetail character, ClassPanel panel, String fallback) {
  for (final c in character.classes) {
    if (c.classIndex == panel.classIndex) return c.className;
  }
  return fallback;
}

// ---------------------------------------------------------------------------
// Generic
// ---------------------------------------------------------------------------

/// Panel for classes without their own: the class and any extra data the
/// server sends. Class resources are listed in the Resources section.
Widget buildGenericPanel(BuildContext context, ClassPanelContext panel) {
  final data = panel.panel.data;
  if (data.isEmpty) return const SizedBox.shrink();
  return Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      SectionHeader(
        '${_className(panel.character, panel.panel, titleFromSpellIndex(panel.panel.classIndex))} '
        '(nivel ${panel.panel.level})',
        padding: combatSectionPadding,
      ),
      CombatCard(
        key: Key('class-panel-${panel.panel.classIndex}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (final e in data.entries)
              if (e.value is! Map && e.value is! List) Text('${e.key}: ${e.value}'),
          ],
        ),
      ),
    ],
  );
}
