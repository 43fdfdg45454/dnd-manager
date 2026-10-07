import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/textures.dart';
import '../../../../core/theme/tokens.dart';
import '../../../catalog/ui/detail_widgets.dart' show Paragraphs;
import '../../data/level_up_controller.dart';
import '../../data/models.dart';
import '../../domain/class_theme.dart';
import 'level_up_widgets.dart';

/// Page 1: "Subes a nivel N", the class that gains the level (the current one
/// preselected; classes the character cannot take are disabled with the
/// reason) and the features gained automatically.
class LevelUpClassStep extends ConsumerWidget {
  const LevelUpClassStep({super.key, required this.characterId});

  final String characterId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(levelUpControllerProvider(characterId));
    final controller = ref.read(levelUpControllerProvider(characterId).notifier);
    final plan = state.plan;
    final theme = Theme.of(context);
    if (plan == null) {
      return Center(
        child: state.loadError == null
            ? const CircularProgressIndicator()
            : Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      state.loadError!,
                      key: const Key('levelup-load-error'),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      key: const Key('levelup-retry'),
                      onPressed: controller.reload,
                      icon: const Icon(Icons.refresh),
                      label: const Text('Reintentar'),
                    ),
                  ],
                ),
              ),
      );
    }
    final features = state.visibleFeatures;
    final selectedIndex = state.selectedClassIndex;
    final selectedName = plan.classes
        .where((c) => c.classIndex == selectedIndex)
        .map((c) => classThemes[c.classIndex]?.labelEs ?? c.name)
        .firstOrNull;
    return LevelUpStepList(
      children: [
        LevelUpHeading(
          'Subes a nivel ${plan.targetLevel}',
          subtitle: plan.classes.length > 1
              ? 'Elige la clase en la que ganas el nivel.'
              : 'Ganas un nivel de ${selectedName ?? plan.classIndex}.',
        ),
        for (final option in plan.classes)
          _ClassCard(
            option: option,
            selected: option.classIndex == selectedIndex,
            onTap: () => controller.selectClass(option.classIndex),
          ),
        if (state.loadError != null && plan.classIndex != selectedIndex)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              state.loadError!,
              key: const Key('levelup-load-error'),
              style: TextStyle(color: theme.colorScheme.error),
            ),
          ),
        const SectionHeader('Rasgos que ganas', padding: EdgeInsets.symmetric(vertical: 12)),
        if (!state.planIsCurrent)
          const Center(
            child: Padding(padding: EdgeInsets.all(16), child: CircularProgressIndicator()),
          )
        else if (features.isEmpty)
          Text(
            'No ganas rasgos automáticos en este nivel.',
            key: const Key('levelup-no-features'),
            style: theme.textTheme.bodyMedium,
          )
        else
          for (final feature in features)
            RuneCard(
              key: Key('levelup-feature-${feature.index}'),
              margin: const EdgeInsets.symmetric(vertical: 4),
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: ExpansionTile(
                tilePadding: EdgeInsets.zero,
                shape: const Border(),
                collapsedShape: const Border(),
                childrenPadding: const EdgeInsets.only(bottom: 8),
                expandedCrossAxisAlignment: CrossAxisAlignment.start,
                title: Text(feature.name),
                subtitle: Text(
                  feature.subclassIndex == null
                      ? 'Nivel ${plan.classLevel}'
                      : 'Subclase · nivel ${plan.classLevel}',
                ),
                children: [
                  feature.description.isEmpty
                      ? const Text('Sin descripción.')
                      : Paragraphs(feature.description),
                ],
              ),
            ),
      ],
    );
  }
}

class _ClassCard extends StatelessWidget {
  const _ClassCard({required this.option, required this.selected, required this.onTap});

  final LevelUpClassOption option;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final classTheme = classThemeOf(option.classIndex);
    final accent = classAccentOf(context, option.classIndex);
    final name = classThemes[option.classIndex]?.labelEs ?? option.name;
    final levels = option.isNew
        ? 'Nueva clase · nivel 1'
        : 'Nivel ${option.currentLevel} → ${option.currentLevel + 1}';
    return Opacity(
      opacity: option.allowed ? 1 : 0.55,
      child: RuneCard(
        key: Key('levelup-class-${option.classIndex}'),
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.all(12),
        color: selected ? tokens.ember.withValues(alpha: 0.16) : null,
        borderColor: selected ? tokens.ember : null,
        onTap: option.allowed ? onTap : null,
        child: Row(
          children: [
            AppIcon(classTheme.icon, size: 32, color: accent),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, style: theme.textTheme.titleMedium),
                  Text('$levels · d${option.hitDie}', style: theme.textTheme.bodySmall),
                  if (!option.allowed && option.reason != null)
                    Text(
                      option.reason!,
                      key: Key('levelup-class-reason-${option.classIndex}'),
                      style: theme.textTheme.bodySmall?.copyWith(color: tokens.blood),
                    ),
                ],
              ),
            ),
            if (selected) Icon(Icons.check_circle, color: tokens.ember),
          ],
        ),
      ),
    );
  }
}
