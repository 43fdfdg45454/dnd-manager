import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/network/api_error.dart';
import '../../../../../core/theme/icons.dart';
import '../../../../../core/ui/action_type.dart';

import '../../../../../core/theme/app_icon.dart';
import '../../../../catalog/data/beast_models.dart';
import '../../../../catalog/data/catalog_controllers.dart';
import '../../../../catalog/ui/beast_page.dart' show BeastTile;
import '../../../data/models.dart';
import '../combat_state.dart';
import '../combat_support.dart';
import '../recovery_reminder.dart';
import 'panel_support.dart';
import 'wizard.dart' show ArcaneRecoveryDialog;

/// Highest beast challenge rating for Wild Shape and its limits (SRD).
({String cr, String limits}) wildShapeLimits(int level, {bool moon = false}) {
  final rules = wildShapeRules(level, moon: moon);
  return (cr: rules.crText, limits: rules.limits);
}

/// Wild Shape by druid level: the highest challenge rating (1/4 at level 2,
/// 1/2 at 4, 1 at 8) and whether flying (level 8) and swimming (level 4)
/// beasts are allowed. Circle of the Moon (Combat Wild Shape, Circle Forms):
/// CR 1 from level 2 and level / 3 from level 6; the movement limits stay.
({double maxCr, String crText, bool fly, bool swim, String limits}) wildShapeRules(
  int level, {
  bool moon = false,
}) {
  final base = level >= 8 ? 1.0 : (level >= 4 ? 0.5 : 0.25);
  final circle = !moon ? 0.0 : (level >= 6 ? (level ~/ 3).toDouble() : 1.0);
  final maxCr = circle > base ? circle : base;
  final crText = switch (maxCr) {
    0.25 => '1/4',
    0.5 => '1/2',
    _ => maxCr.toInt().toString(),
  };
  return (
    maxCr: maxCr,
    crText: crText,
    fly: level >= 8,
    swim: level >= 4,
    limits: level >= 8
        ? 'Sin limitaciones de movimiento.'
        : level >= 4
        ? 'Sin velocidad de vuelo.'
        : 'Sin velocidad de vuelo ni de nado.',
  );
}

/// Whether the druid follows the Circle of the Moon (its subclass index
/// mentions "moon": it comes from a content pack, the SRD only has Land).
bool isCircleOfTheMoon(CharacterDetail character) => character.classes.any(
  (k) => k.classIndex == 'druid' && (k.subclassIndex ?? '').toLowerCase().contains('moon'),
);

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
    final moon = isCircleOfTheMoon(c);
    final limits = wildShapeLimits(level, moon: moon);
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
          // SRD: "you can use your action to magically assume the shape of a beast".
          actionKind: ActionKind.action,
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
              WildShapeForms(level: level, moon: moon),
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

/// "Formas disponibles": the SRD beasts the druid can become at [level]
/// (challenge rating and movement limits); each one opens its statblock.
class WildShapeForms extends ConsumerWidget {
  const WildShapeForms({super.key, required this.level, this.moon = false});

  final int level;
  final bool moon;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rules = wildShapeRules(level, moon: moon);
    // Only the forbidden speeds are filtered out.
    final BeastQuery query = (
      maxCr: rules.maxCr,
      fly: rules.fly ? null : false,
      swim: rules.swim ? null : false,
    );
    final beasts = ref.watch(beastsProvider(query));
    final count = beasts.value?.length;
    return ExpansionTile(
      key: const Key('druid-wild-shape-forms'),
      tilePadding: EdgeInsets.zero,
      title: Text(count == null ? 'Formas disponibles' : 'Formas disponibles ($count)'),
      subtitle: Text('Bestias de VD ${rules.crText} o menos'),
      children: [
        beasts.when(
          loading: () => const Padding(
            padding: EdgeInsets.all(12),
            child: Center(child: CircularProgressIndicator()),
          ),
          error: (error, _) => ListTile(
            title: Text(describeApiError(error)),
            trailing: IconButton(
              tooltip: 'Reintentar',
              icon: const Icon(Icons.refresh),
              onPressed: () => ref.invalidate(beastsProvider(query)),
            ),
          ),
          data: (list) => list.isEmpty
              ? const ListTile(title: Text('No hay bestias que cumplan los límites.'))
              : Column(children: [for (final b in list) BeastTile(beast: b)]),
        ),
      ],
    );
  }
}
