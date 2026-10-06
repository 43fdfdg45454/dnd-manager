import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/motion/motion_settings.dart';
import '../../../core/theme/app_theme.dart';
import '../data/appearance_controller.dart';

/// "Personalización": theme, text size and animation level of this device.
class AppearancePage extends ConsumerWidget {
  const AppearancePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(appearanceProvider);
    final motion = ref.watch(motionSettingsProvider);
    final appearance = ref.read(appearanceProvider.notifier);
    final theme = Theme.of(context);
    return Scaffold(
      key: const Key('settings-appearance'),
      appBar: AppBar(title: const Text('Personalización')),
      body: GrainBackground(
        child: ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            const SectionHeader('Tema'),
            _Choice<ThemeMode>(
              key: const Key('appearance-theme'),
              selected: settings.themeMode,
              onChanged: appearance.selectThemeMode,
              options: const [
                (ThemeMode.dark, 'Oscuro', AppIcons.moon, 'appearance-theme-dark'),
                (ThemeMode.light, 'Claro', AppIcons.sun, 'appearance-theme-light'),
                (ThemeMode.system, 'Sistema', AppIcons.gear, 'appearance-theme-system'),
              ],
            ),
            const SectionHeader('Tamaño de texto'),
            _Choice<TextSizePreference>(
              key: const Key('appearance-text-size'),
              selected: settings.textSize,
              onChanged: appearance.selectTextSize,
              options: const [
                (TextSizePreference.normal, 'Normal', AppIcons.book, 'appearance-text-normal'),
                (TextSizePreference.large, 'Grande', AppIcons.scroll, 'appearance-text-large'),
              ],
            ),
            const SectionHeader('Animaciones'),
            _Choice<MotionPreference>(
              key: const Key('appearance-motion'),
              selected: motion,
              onChanged: ref.read(motionSettingsProvider.notifier).select,
              options: const [
                (MotionPreference.all, 'Todas', AppIcons.sparkles, 'appearance-motion-all'),
                (
                  MotionPreference.reduced,
                  'Reducidas',
                  AppIcons.anchor,
                  'appearance-motion-reduced',
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: Text(
                'Con «Reducidas» los efectos no se animan: se muestra directamente su resultado.',
                style: theme.textTheme.bodySmall,
              ),
            ),
            const SectionHeader('Vista previa'),
            const _Preview(),
          ],
        ),
      ),
    );
  }
}

/// A full-width segmented choice among [options] (value, label, icon, key).
class _Choice<T> extends StatelessWidget {
  const _Choice({
    super.key,
    required this.selected,
    required this.onChanged,
    required this.options,
  });

  final T selected;
  final ValueChanged<T> onChanged;
  final List<(T, String, AppIcons, String)> options;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: SegmentedButton<T>(
        showSelectedIcon: false,
        expandedInsets: EdgeInsets.zero,
        segments: [
          for (final (value, label, icon, key) in options)
            ButtonSegment<T>(
              value: value,
              icon: AppIcon(icon, size: 18),
              label: Text(label, key: Key(key)),
            ),
        ],
        selected: {selected},
        onSelectionChanged: (values) => onChanged(values.single),
      ),
    );
  }
}

/// Sample card in the current theme, text size and motion level.
class _Preview extends StatelessWidget {
  const _Preview();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    return RuneCard(
      key: const Key('appearance-preview'),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              AppIcon(AppIcons.wizard, color: tokens.arcane),
              const SizedBox(width: 8),
              Expanded(child: Text('Personaje de ejemplo', style: theme.textTheme.titleMedium)),
              AppIcon(AppIcons.seal, size: 20, color: tokens.oldGold),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Así se verán las tarjetas, los textos y los números de la hoja.',
            style: theme.textTheme.bodyMedium,
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              AppIcon(AppIcons.heart, size: 20, color: tokens.blood),
              const SizedBox(width: 6),
              Text('27 / 31', style: theme.textTheme.titleMedium?.merge(AppTypography.numeric)),
              const SizedBox(width: 16),
              AppIcon(AppIcons.shield, size: 20, color: tokens.oldGold),
              const SizedBox(width: 6),
              Text('15', style: theme.textTheme.titleMedium?.merge(AppTypography.numeric)),
              const Spacer(),
              FilledButton(onPressed: () {}, child: const Text('Acción')),
            ],
          ),
        ],
      ),
    );
  }
}
