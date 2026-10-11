import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../domain/class_theme.dart';
import '../../models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_rows.dart';
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

/// The rows of each class by `classIndex`. A class without an entry gets
/// [genericClassPanel]. Register new classes here.
final Map<String, ClassPanelSpec> classPanelSpecs = {
  'barbarian': barbarianPanel,
  'bard': bardPanel,
  'cleric': clericPanel,
  'druid': druidPanel,
  'fighter': fighterPanel,
  'monk': monkPanel,
  'paladin': paladinPanel,
  'ranger': rangerPanel,
  'rogue': roguePanel,
  'sorcerer': sorcererPanel,
  'warlock': warlockPanel,
  'wizard': wizardPanel,
};

/// Classes whose rows are built from the server's `ClassPanelDto`: without it
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
          classPanelSpecs.containsKey(c.classIndex))
        ClassPanel(classIndex: c.classIndex, level: c.level),
    for (final panel in character.combat.classPanels)
      if (!classes.contains(panel.classIndex)) panel,
  ];
}

/// The tab of the combat view a group of class rows goes to.
enum ClassPanelPart { actions, spells, more }

/// Whether some class of [character] has rows for "Conjuros".
bool hasClassSpellRows(CharacterDetail character) =>
    classPanelsOf(character).any((p) => classPanelSpecs[p.classIndex]?.spells != null);

/// One group per class with its rows for [part], each with the accent of its
/// class. The "Acciones" group is keyed `class-panel-<index>`, the others
/// `class-<part>-<index>`.
class ClassPanelGroups extends ConsumerWidget {
  const ClassPanelGroups({
    super.key,
    required this.character,
    required this.part,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;
  final ClassPanelPart part;
  final bool canEdit;
  final bool isDm;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groups = <Widget>[];
    for (final panel in classPanelsOf(character)) {
      final index = panel.classIndex;
      final spec = classPanelSpecs[index] ?? genericClassPanel;
      final builder = switch (part) {
        ClassPanelPart.actions => spec.actions,
        ClassPanelPart.spells => spec.spells,
        ClassPanelPart.more => spec.more,
      };
      if (builder == null) continue;
      final rows = builder(
        ClassPanelContext(character: character, panel: panel, canEdit: canEdit, isDm: isDm),
        ref,
      );
      if (rows.isEmpty) continue;
      final known = classPanelSpecs.containsKey(index);
      final theme = classThemeOf(index);
      final name = known ? theme.labelEs : _className(character, panel);
      groups.add(
        ClassAccent(
          classIndex: index,
          child: Builder(
            builder: (context) => CombatGroup(
              key: Key(
                part == ClassPanelPart.actions ? 'class-panel-$index' : 'class-${part.name}-$index',
              ),
              leading: AppIcon(
                known ? theme.icon : AppIcons.d20,
                size: 22,
                color: Theme.of(context).colorScheme.primary,
              ),
              title: '$name (nivel ${panel.level})',
              children: rows,
            ),
          ),
        ),
      );
    }
    if (groups.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: groups);
  }
}

String _className(CharacterDetail character, ClassPanel panel) {
  for (final c in character.classes) {
    if (c.classIndex == panel.classIndex) return c.className;
  }
  return titleFromSpellIndex(panel.classIndex);
}

// ---------------------------------------------------------------------------
// Generic
// ---------------------------------------------------------------------------

/// Rows for classes without their own: any extra data the server sends, in
/// "Más". Class resources are listed in "Recursos".
const genericClassPanel = ClassPanelSpec(actions: _noRows, more: genericClassRows);

List<Widget> _noRows(ClassPanelContext panel, WidgetRef ref) => const [];

List<Widget> genericClassRows(ClassPanelContext panel, WidgetRef ref) => [
  for (final e in panel.panel.data.entries)
    if (e.value is! Map && e.value is! List) CombatActionRow(title: '${e.key}: ${e.value}'),
];
