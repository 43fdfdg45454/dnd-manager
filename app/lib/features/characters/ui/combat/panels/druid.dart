import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/icons.dart';

import '../../../../../core/theme/app_icon.dart';
import '../../../data/models.dart';
import '../combat_state.dart';
import '../combat_support.dart';
import '../recovery_reminder.dart';
import 'panel_support.dart';
import 'wizard.dart' show ArcaneRecoveryDialog;

/// Highest beast challenge rating for Wild Shape and its limits (SRD).
({String cr, String limits}) wildShapeLimits(int level) => level >= 8
    ? (cr: '1', limits: 'Sin limitaciones de movimiento.')
    : level >= 4
    ? (cr: '1/2', limits: 'Sin velocidad de vuelo.')
    : (cr: '1/4', limits: 'Sin velocidad de vuelo ni de nado.');

/// Druid: Wild Shape (resource `wild-shape`, from level 2), the beast limits
/// by level and a local "in Wild Shape" switch.
class DruidPanel extends ConsumerWidget {
  const DruidPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final level = panel.level;
    final limits = wildShapeLimits(level);
    final active = ref.watch(wildShapeControllerProvider(c.id));
    final form = ref.read(wildShapeControllerProvider(c.id).notifier);
    // Circle of the Land: the server only sends the data when the druid has it.
    final recovery = panelMap(panel.panel.data['naturalRecovery']);
    final recovered = recovery['used'] == true;
    final budget = panelInt(recovery['slotLevelsRecoverable']) ?? (level + 1) ~/ 2;

    Future<void> naturalRecovery() async {
      final levels = await showDialog<List<int>>(
        context: context,
        builder: (_) => ArcaneRecoveryDialog(
          character: c,
          budget: naturalRecoveryBudget(c) ?? budget,
          title: SlotRecovery.natural.label,
        ),
      );
      if (levels == null || levels.isEmpty || !context.mounted) return;
      await runCombat(
        context,
        () => SlotRecovery.natural.use(ref, c, levels),
        success: 'Recuperación natural aplicada.',
      );
    }

    Future<void> transform(BuildContext context, WidgetRef ref, CharacterResource resource) async {
      final done = await spendClassResource(
        context,
        ref,
        c,
        resource,
        success: 'Adoptas tu forma salvaje.',
      );
      if (done) form.set(true);
    }

    return ClassPanelFrame(
      panel: panel,
      children: [
        ClassResourceActionCard(
          panel: panel,
          resourceKey: 'wild-shape',
          title: 'Forma salvaje',
          actionKey: 'druid-wild-shape',
          buttonLabel: 'Adoptar forma salvaje',
          icon: AppIcons.druid,
          minLevel: 2,
          onUse: transform,
          extra: [
            if (level >= 2) ...[
              PanelFact(
                key: const Key('druid-wild-shape-cr'),
                label: 'VD máximo',
                value: limits.cr,
                detail: '${limits.limits} Duración: hasta ${level ~/ 2} h.',
              ),
              SwitchListTile(
                key: const Key('druid-wild-shape-active'),
                contentPadding: EdgeInsets.zero,
                title: const Text('En forma salvaje'),
                subtitle: Text(
                  active
                      ? 'Usas los PG de la bestia; al llegar a 0 vuelves a tu forma.'
                      : 'Solo en este dispositivo.',
                ),
                value: active,
                onChanged: panel.canEdit ? form.set : null,
              ),
              if (level >= 20)
                Text(
                  'Archidruida: usos ilimitados de forma salvaje.',
                  style: theme.textTheme.bodySmall,
                ),
            ],
          ],
        ),
        if (recovery.isNotEmpty)
          CombatCard(
            key: const Key('druid-natural-recovery-card'),
            title: 'Recuperación natural',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.tonalIcon(
                    key: const Key('natural-recovery'),
                    onPressed: panel.canEdit && !recovered ? naturalRecovery : null,
                    icon: const AppIcon(AppIcons.druid, size: 20),
                    label: Text(
                      recovered ? 'Recuperación natural (usada)' : 'Recuperación natural',
                    ),
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Tras un descanso corto, recupera espacios de conjuro cuyo nivel sumado sea '
                  '$budget como máximo (ninguno de nivel 6 o superior). Una vez por día.',
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ),
      ],
    );
  }
}
