import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../catalog/data/catalog_controllers.dart';
import '../../../../catalog/data/models.dart' show Feature;

import '../combat_rows.dart';
import '../combat_support.dart';
import 'panel_support.dart';

/// Eldritch Invocations known by warlock level (SRD); 0 before level 2.
int invocationsKnown(int level) => level >= 18
    ? 8
    : level >= 15
    ? 7
    : level >= 12
    ? 6
    : level >= 9
    ? 5
    : level >= 7
    ? 4
    : level >= 5
    ? 3
    : level >= 2
    ? 2
    : 0;

/// Mystic Arcanum spell levels unlocked at warlock [level] (6th at 11 ... 9th at 17).
List<int> mysticArcanum(int level) => [
  if (level >= 11) 6,
  if (level >= 13) 7,
  if (level >= 15) 8,
  if (level >= 17) 9,
];

/// Warlock: the Eldritch Invocations from the class features and Mystic
/// Arcanum to remember. The pact slots are with the other slots in
/// "Conjuros".
const warlockPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) => const [];

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final level = panel.level;
  final known = invocationsKnown(level);
  final arcanum = mysticArcanum(level);
  final detail = known > 0 ? ref.watch(classDetailProvider('warlock')).value : null;
  final invocations = <Feature>[
    if (detail != null)
      for (final l in detail.levels)
        if (l.level <= level)
          for (final f in l.features)
            if (f.index.contains('invocation')) f,
  ];
  return [
    if (known > 0)
      CombatActionRow(
        icon: AppIcons.warlock,
        title: 'Invocaciones sobrenaturales',
        cost: 'Conocidas: $known',
        costKey: const Key('warlock-invocations-known'),
        subtitle: invocations.isEmpty ? 'Consulta tus invocaciones en la pestaña Rasgos.' : null,
        info: const FeatureInfoButton(index: 'eldritch-invocations'),
        details: [
          for (final f in invocations)
            ExpansionTile(
              key: Key('warlock-invocation-${f.index}'),
              tilePadding: EdgeInsets.zero,
              dense: true,
              title: Text(f.name),
              childrenPadding: const EdgeInsets.only(bottom: 8),
              expandedCrossAxisAlignment: CrossAxisAlignment.start,
              children: [for (final p in f.description) Text(p)],
            ),
        ],
      ),
    if (arcanum.isNotEmpty)
      CombatActionRow(
        icon: AppIcons.warlock,
        title: 'Arcano místico',
        subtitle: 'Un conjuro de nivel ${arcanum.join(', ')} una vez por descanso largo cada uno.',
        subtitleKey: const Key('warlock-mystic-arcanum'),
        info: const FeatureInfoButton(index: 'mystic-arcanum-6th-level'),
      ),
  ];
}
