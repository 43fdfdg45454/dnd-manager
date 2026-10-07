import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/motion/motion_settings.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/ui/stat_value.dart';
import '../../characters/data/models.dart';
import '../../characters/domain/class_theme.dart';
import '../data/appearance_controller.dart';

/// "Personalización": palette, mode, fonts, text size, class colours,
/// textures and animation level of this device. A live preview on top
/// follows every choice; everything applies at once and is stored locally.
class AppearancePage extends ConsumerWidget {
  const AppearancePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(appearanceProvider);
    final motion = ref.watch(motionSettingsProvider);
    final appearance = ref.read(appearanceProvider.notifier);
    final theme = Theme.of(context);
    final brightness = _effectiveBrightness(context, settings.themeMode);
    return Scaffold(
      key: const Key('settings-appearance'),
      appBar: AppBar(
        title: const Text('Personalización'),
        actions: [
          TextButton(
            key: const Key('appearance-reset'),
            style: TextButton.styleFrom(foregroundColor: context.tokens.bone),
            onPressed: () {
              appearance.reset();
              ref.read(motionSettingsProvider.notifier).reset();
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(const SnackBar(content: Text('Personalización restablecida.')));
            },
            child: const Text('Restablecer'),
          ),
        ],
      ),
      body: GrainBackground(
        // Ink of the tiles below is drawn on this transparent Material, above
        // the painted background.
        child: Material(
          type: MaterialType.transparency,
          child: Column(
            children: [
              _Preview(settings: settings, brightness: brightness),
              Expanded(
                child: ListView(
                  key: const Key('appearance-options'),
                  padding: const EdgeInsets.only(bottom: 24),
                  children: [
                    const SectionHeader('Paleta'),
                    _PaletteGrid(
                      selected: settings.palette,
                      brightness: brightness,
                      onChanged: appearance.selectPalette,
                    ),
                    const SectionHeader('Modo'),
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
                    const SectionHeader('Fuente de títulos'),
                    for (final font in TitleFont.values)
                      _FontOption(
                        key: Key('title-font-${font.name}'),
                        label: font.label,
                        sample: 'La cripta del rey olvidado',
                        style: AppTypography.title(
                          fonts: AppFontSet(title: font, body: settings.bodyFont),
                        ),
                        selected: settings.titleFont == font,
                        onTap: () => appearance.selectTitleFont(font),
                      ),
                    const SectionHeader('Fuente de texto'),
                    for (final font in BodyFont.values)
                      _FontOption(
                        key: Key('body-font-${font.name}'),
                        label: font.label,
                        sample: 'Tira 1d20 + 5 para atacar: 17 contra CA 15.',
                        style: AppTypography.sans(
                          fonts: AppFontSet(title: settings.titleFont, body: font),
                        ),
                        selected: settings.bodyFont == font,
                        onTap: () => appearance.selectBodyFont(font),
                      ),
                    const SectionHeader('Tamaño de texto'),
                    _Choice<TextSizePreference>(
                      key: const Key('appearance-text-size'),
                      selected: settings.textSize,
                      onChanged: appearance.selectTextSize,
                      options: const [
                        (
                          TextSizePreference.normal,
                          'Normal',
                          AppIcons.book,
                          'appearance-text-normal',
                        ),
                        (
                          TextSizePreference.large,
                          'Grande',
                          AppIcons.scroll,
                          'appearance-text-large',
                        ),
                        (
                          TextSizePreference.extraLarge,
                          'Muy grande',
                          AppIcons.spellbook,
                          'appearance-text-xlarge',
                        ),
                      ],
                    ),
                    const SectionHeader('Superficies'),
                    SwitchListTile(
                      key: const Key('appearance-class-colors'),
                      value: settings.classColors,
                      onChanged: appearance.setClassColors,
                      title: const Text('Colores de clase'),
                      subtitle: Text(
                        settings.palette.recommendsClassColors
                            ? 'Las tarjetas y paneles se tiñen con el color de la clase.'
                            : 'Las tarjetas y paneles se tiñen con el color de la clase. '
                                  'Con ${settings.palette.label} se recomienda desactivarlo.',
                      ),
                    ),
                    SwitchListTile(
                      key: const Key('appearance-textures'),
                      value: settings.textures,
                      onChanged: appearance.setTextures,
                      title: const Text('Texturas'),
                      subtitle: const Text(
                        'Grano de piedra en los fondos y bordes a pincel en las tarjetas.',
                      ),
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
                        'Con «Reducidas» los efectos no se animan: se muestra directamente su '
                        'resultado.',
                        style: theme.textTheme.bodySmall,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The brightness [mode] resolves to on this device.
Brightness _effectiveBrightness(BuildContext context, ThemeMode mode) => switch (mode) {
  ThemeMode.dark => Brightness.dark,
  ThemeMode.light => Brightness.light,
  ThemeMode.system => MediaQuery.platformBrightnessOf(context),
};

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

/// Two columns of palette cards, each painted in its own colours for
/// [brightness].
class _PaletteGrid extends StatelessWidget {
  const _PaletteGrid({required this.selected, required this.brightness, required this.onChanged});

  final AppPalette selected;
  final Brightness brightness;
  final ValueChanged<AppPalette> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: LayoutBuilder(
        builder: (context, constraints) {
          const gap = 12.0;
          final columns = constraints.maxWidth >= 520 ? 3 : 2;
          final width = (constraints.maxWidth - gap * (columns - 1)) / columns;
          return Wrap(
            spacing: gap,
            runSpacing: gap,
            children: [
              for (final palette in AppPalette.values)
                SizedBox(
                  width: width,
                  child: _PaletteCard(
                    palette: palette,
                    brightness: brightness,
                    selected: palette == selected,
                    onTap: () => onChanged(palette),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

/// A palette sample: page background with a card, text, the primary and the
/// semantic colours (healing, damage, magic).
class _PaletteCard extends StatelessWidget {
  const _PaletteCard({
    required this.palette,
    required this.brightness,
    required this.selected,
    required this.onTap,
  });

  final AppPalette palette;
  final Brightness brightness;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final t = palette.tokens(brightness);
    final current = Theme.of(context).colorScheme;
    final text = Theme.of(context).textTheme;
    final scheme = AppTheme.scheme(palette, brightness);
    final border = selected ? current.primary : t.rune;
    Widget dot(Color color, String label) => Tooltip(
      message: label,
      child: Container(
        width: 14,
        height: 14,
        decoration: BoxDecoration(
          color: color,
          shape: BoxShape.circle,
          border: Border.all(color: t.bone.withValues(alpha: 0.35)),
        ),
      ),
    );
    return Semantics(
      selected: selected,
      button: true,
      label: 'Paleta ${palette.label}',
      excludeSemantics: true,
      child: Material(
        key: Key('palette-${palette.name}'),
        color: t.obsidian,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(8),
          side: BorderSide(color: border, width: selected ? 2.5 : 1),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        palette.label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: text.titleSmall?.copyWith(color: t.bone),
                      ),
                    ),
                    if (selected) Icon(Icons.check_circle, size: 18, color: t.bone),
                  ],
                ),
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
                  decoration: BoxDecoration(
                    color: t.stone,
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(color: t.oldGold.withValues(alpha: 0.5)),
                  ),
                  child: Row(
                    children: [
                      Text('Aa', style: text.labelLarge?.copyWith(color: t.bone)),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'texto',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: text.bodySmall?.copyWith(color: t.boneMuted),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: t.ember,
                          borderRadius: BorderRadius.circular(3),
                        ),
                        child: Text(
                          'OK',
                          style: text.labelSmall?.copyWith(color: scheme.onPrimary),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    dot(t.moss, 'Curación'),
                    const SizedBox(width: 6),
                    dot(t.blood, 'Daño'),
                    const SizedBox(width: 6),
                    dot(t.arcane, 'Magia'),
                    const SizedBox(width: 6),
                    dot(t.oldGold, 'Detalles'),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A selectable font: its name and a sample, both in [style].
class _FontOption extends StatelessWidget {
  const _FontOption({
    super.key,
    required this.label,
    required this.sample,
    required this.style,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final String sample;
  final TextStyle style;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    return Semantics(
      selected: selected,
      button: true,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            children: [
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_unchecked,
                size: 22,
                color: selected ? theme.colorScheme.primary : tokens.boneMuted,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      label,
                      style: theme.textTheme.titleMedium?.merge(style).copyWith(color: tokens.bone),
                    ),
                    Text(
                      sample,
                      style: theme.textTheme.bodyMedium
                          ?.merge(style)
                          .copyWith(color: tokens.boneMuted, fontWeight: FontWeight.w400),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A miniature character card in the chosen palette, fonts, class colours and
/// textures: class icon and name, hit points, a [StatValue] and a button.
/// Built in its own theme so it follows the settings even before the app
/// theme around it rebuilds.
class _Preview extends StatelessWidget {
  const _Preview({required this.settings, required this.brightness});

  final AppearanceSettings settings;
  final Brightness brightness;

  static const _classIndex = 'wizard';

  static const _armorClass = ValueBreakdown(
    total: 15,
    parts: [
      BreakdownPart(source: 'base', label: 'Base', value: 10),
      BreakdownPart(source: 'ability', label: 'Destreza', value: 3),
      BreakdownPart(source: 'item', label: 'Anillo de protección', value: 2),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final fonts = settings.fonts;
    final data = AppTheme.build(
      brightness,
      palette: settings.palette,
      fonts: fonts,
      style: settings.style,
      activateFonts: false,
    );
    final tokens = data.extension<AppTokens>()!;
    final numeric = AppTypography.numericOf(fonts);
    return Theme(
      data: data,
      child: Builder(
        builder: (context) {
          final theme = Theme.of(context);
          final accent = classAccentOf(context, _classIndex);
          return GrainBackground(
            key: const Key('appearance-preview-area'),
            child: Padding(
              padding: const EdgeInsets.only(top: 4, bottom: 4),
              child: RuneCard(
                key: const Key('appearance-preview'),
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        AppIcon(
                          classThemeOf(_classIndex).icon,
                          key: const Key('appearance-preview-class-icon'),
                          color: accent,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Personaje de ejemplo',
                                key: const Key('appearance-preview-title'),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: theme.textTheme.titleMedium,
                              ),
                              Text(
                                'Mago · nivel 5',
                                style: theme.textTheme.bodySmall?.copyWith(color: accent),
                              ),
                            ],
                          ),
                        ),
                        const Text('CA '),
                        StatValue(
                          statKey: 'preview.ac',
                          text: '15',
                          title: 'Clase de armadura',
                          breakdown: _armorClass,
                          style: theme.textTheme.titleMedium?.merge(numeric),
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        AppIcon(AppIcons.heart, size: 18, color: tokens.bloodText),
                        const SizedBox(width: 6),
                        Text(
                          'PG',
                          style: theme.textTheme.labelLarge?.copyWith(color: tokens.boneMuted),
                        ),
                        const SizedBox(width: 6),
                        Text('27 / 31', style: theme.textTheme.titleMedium?.merge(numeric)),
                        const SizedBox(width: 10),
                        Expanded(
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(3),
                            child: LinearProgressIndicator(
                              value: 27 / 31,
                              minHeight: 6,
                              color: tokens.mossText,
                              backgroundColor: tokens.stoneRaised,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        Expanded(
                          child: Text.rich(
                            TextSpan(
                              children: [
                                TextSpan(
                                  text: '+8 curación',
                                  style: TextStyle(color: tokens.mossText),
                                ),
                                const TextSpan(text: ' · '),
                                TextSpan(
                                  text: '−5 daño',
                                  style: TextStyle(color: tokens.bloodText),
                                ),
                                const TextSpan(text: ' · '),
                                TextSpan(
                                  text: 'magia',
                                  style: TextStyle(color: tokens.arcaneText),
                                ),
                              ],
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: theme.textTheme.labelLarge,
                          ),
                        ),
                        const SizedBox(width: 8),
                        FilledButton(
                          key: const Key('appearance-preview-action'),
                          onPressed: () {},
                          child: const Text('Lanzar conjuro'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
