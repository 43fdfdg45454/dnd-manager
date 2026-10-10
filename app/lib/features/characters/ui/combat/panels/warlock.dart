import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../catalog/data/catalog_controllers.dart';
import '../../../../catalog/data/models.dart' show Feature;
import '../combat_support.dart';
import '../resources_section.dart' show pactSlotsOf;
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

/// Warlock: pact slots (`combat.pactSlots`, tap spends, long press restores),
/// the Eldritch Invocations from the class features and Mystic Arcanum.
class WarlockPanel extends ConsumerWidget {
  const WarlockPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final level = panel.level;
    final pact = pactSlotsOf(c);
    final known = invocationsKnown(level);
    final arcanum = mysticArcanum(level);
    final detail = ref.watch(classDetailProvider('warlock')).value;
    final invocations = <Feature>[
      if (detail != null)
        for (final l in detail.levels)
          if (l.level <= level)
            for (final f in l.features)
              if (f.index.contains('invocation')) f,
    ];

    Future<void> spend() async {
      if (pact == null) return;
      if (pact.used >= pact.max) {
        showCombatMessage(context, 'No quedan espacios de pacto.');
        return;
      }
      await runCombat(context, () => panelController(ref, c).spendSpellSlot(0));
    }

    Future<void> restore() async {
      if (pact == null || pact.used <= 0) return;
      await runCombat(context, () => panelController(ref, c).restoreSpellSlot(0));
    }

    return ClassPanelFrame(
      panel: panel,
      children: [
        CombatCard(
          title: 'Magia del pacto',
          featureIndex: 'pact-magic',
          trailing: pact == null || pact.level <= 0
              ? null
              : Text('Nivel ${pact.level}', key: const Key('warlock-pact-level')),
          child: pact == null
              ? Row(
                  key: const Key('warlock-pact-slots-unavailable'),
                  children: [
                    Icon(Icons.info_outline, size: 18, color: theme.colorScheme.onSurfaceVariant),
                    const SizedBox(width: 6),
                    Text('Recurso no disponible', style: theme.textTheme.bodySmall),
                  ],
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text('Espacios de pacto', style: theme.textTheme.bodyLarge),
                        ),
                        Text(
                          '${pact.max - pact.used} / ${pact.max}',
                          key: const Key('warlock-pact-slots-uses'),
                          style: theme.textTheme.titleSmall,
                        ),
                      ],
                    ),
                    PipRow(
                      key: const Key('warlock-pact-slots'),
                      total: pact.max,
                      filled: pact.max - pact.used,
                      semanticLabel: 'Espacios de pacto: ${pact.max - pact.used} de ${pact.max}',
                      onTap: panel.canEdit ? spend : null,
                      onLongPress: panel.canEdit ? restore : null,
                    ),
                    Text(
                      'Se recuperan con un descanso corto o largo.',
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ),
        ),
        if (known > 0)
          CombatCard(
            title: 'Invocaciones sobrenaturales',
            featureIndex: 'eldritch-invocations',
            trailing: Text('Conocidas: $known', key: const Key('warlock-invocations-known')),
            child: invocations.isEmpty
                ? Text(
                    'Consulta tus invocaciones en la pestaña Rasgos.',
                    style: theme.textTheme.bodySmall,
                  )
                : Column(
                    children: [
                      for (final f in invocations)
                        ExpansionTile(
                          key: Key('warlock-invocation-${f.index}'),
                          tilePadding: EdgeInsets.zero,
                          title: Text(f.name),
                          childrenPadding: const EdgeInsets.only(bottom: 8),
                          expandedCrossAxisAlignment: CrossAxisAlignment.start,
                          children: [for (final p in f.description) Text(p)],
                        ),
                    ],
                  ),
          ),
        if (arcanum.isNotEmpty)
          CombatCard(
            title: 'Arcano místico',
            featureIndex: 'mystic-arcanum-6th-level',
            child: Text(
              'Un conjuro de nivel ${arcanum.join(', ')} una vez por descanso largo cada uno.',
              key: const Key('warlock-mystic-arcanum'),
            ),
          ),
      ],
    );
  }
}
