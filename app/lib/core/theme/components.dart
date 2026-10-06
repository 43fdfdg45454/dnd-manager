import 'package:flutter/material.dart';

import 'tokens.dart';
import 'typography.dart';

/// Component themes built from the [AppTokens] of one brightness.
class AppComponentThemes {
  AppComponentThemes(this.tokens, this.scheme, this.textTheme);

  final AppTokens tokens;
  final ColorScheme scheme;
  final TextTheme textTheme;

  bool get _dark => scheme.brightness == Brightness.dark;

  Color get _goldBorder => tokens.gold.withValues(alpha: 0.4);

  static const _radius = 10.0;

  CardThemeData get card => CardThemeData(
    color: tokens.parchmentDeep,
    surfaceTintColor: Colors.transparent,
    elevation: 1,
    margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(_radius),
      side: BorderSide(color: _goldBorder),
    ),
  );

  AppBarTheme get appBar => AppBarTheme(
    centerTitle: true,
    backgroundColor: tokens.stone,
    foregroundColor: tokens.ink,
    surfaceTintColor: Colors.transparent,
    scrolledUnderElevation: 0,
    titleTextStyle: textTheme.titleLarge?.copyWith(fontSize: 20, color: tokens.ink),
    shape: Border(bottom: BorderSide(color: _goldBorder)),
  );

  FilledButtonThemeData get filledButton => FilledButtonThemeData(
    style: FilledButton.styleFrom(
      backgroundColor: tokens.crimson,
      foregroundColor: scheme.onPrimary,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    ),
  );

  OutlinedButtonThemeData get outlinedButton => OutlinedButtonThemeData(
    style: OutlinedButton.styleFrom(
      foregroundColor: tokens.ink,
      side: BorderSide(color: tokens.gold),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    ),
  );

  TextButtonThemeData get textButton => TextButtonThemeData(
    // Crimson is too dark to read as text on the dark parchment.
    style: TextButton.styleFrom(foregroundColor: _dark ? tokens.gold : tokens.crimson),
  );

  FloatingActionButtonThemeData get floatingActionButton => FloatingActionButtonThemeData(
    backgroundColor: tokens.crimson,
    foregroundColor: scheme.onPrimary,
  );

  TabBarThemeData get tabBar => TabBarThemeData(
    indicator: UnderlineTabIndicator(borderSide: BorderSide(color: tokens.gold, width: 3)),
    indicatorSize: TabBarIndicatorSize.tab,
    labelColor: tokens.ink,
    unselectedLabelColor: tokens.inkMuted,
    labelStyle: const TextStyle(fontFamily: AppFonts.display, fontWeight: FontWeight.w700),
    unselectedLabelStyle: const TextStyle(
      fontFamily: AppFonts.display,
      fontWeight: FontWeight.w400,
    ),
    dividerColor: tokens.rune.withValues(alpha: 0.5),
  );

  ChipThemeData get chip => ChipThemeData(
    side: BorderSide(color: tokens.rune),
    labelStyle: textTheme.labelLarge,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
  );

  NavigationBarThemeData get navigationBar => NavigationBarThemeData(
    backgroundColor: tokens.stone,
    surfaceTintColor: Colors.transparent,
    indicatorColor: tokens.gold.withValues(alpha: 0.3),
    labelTextStyle: WidgetStatePropertyAll(
      TextStyle(fontFamily: AppFonts.body, fontWeight: FontWeight.w700, color: tokens.ink),
    ),
  );

  ProgressIndicatorThemeData get progress =>
      ProgressIndicatorThemeData(linearTrackColor: tokens.stone, circularTrackColor: tokens.stone);

  DividerThemeData get divider => DividerThemeData(color: tokens.rune, thickness: 1, space: 1);

  SnackBarThemeData get snackBar => SnackBarThemeData(
    backgroundColor: tokens.ink,
    contentTextStyle: TextStyle(fontFamily: AppFonts.body, fontSize: 15, color: tokens.parchment),
    actionTextColor: _dark ? tokens.crimson : tokens.gold,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
  );

  DialogThemeData get dialog => DialogThemeData(
    backgroundColor: tokens.parchment,
    surfaceTintColor: Colors.transparent,
    titleTextStyle: textTheme.titleLarge?.copyWith(color: tokens.ink),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(12),
      side: BorderSide(color: _goldBorder),
    ),
  );
}

/// Parchment-coloured card: the default surface for content.
class ParchmentCard extends StatelessWidget {
  const ParchmentCard({super.key, required this.child, this.margin, this.padding, this.onTap});

  final Widget child;

  /// Defaults to the card theme's margin.
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return _TokenCard(
      color: context.tokens.parchmentDeep,
      margin: margin,
      padding: padding,
      onTap: onTap,
      child: child,
    );
  }
}

/// Stone-coloured card for secondary content.
class StoneCard extends StatelessWidget {
  const StoneCard({super.key, required this.child, this.margin, this.padding, this.onTap});

  final Widget child;
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return _TokenCard(
      color: tokens.stone,
      borderColor: tokens.rune.withValues(alpha: 0.5),
      margin: margin,
      padding: padding,
      onTap: onTap,
      child: child,
    );
  }
}

class _TokenCard extends StatelessWidget {
  const _TokenCard({
    required this.color,
    required this.child,
    this.borderColor,
    this.margin,
    this.padding,
    this.onTap,
  });

  final Color color;
  final Color? borderColor;
  final Widget child;
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final shape = Theme.of(context).cardTheme.shape;
    final radius = shape is RoundedRectangleBorder
        ? shape.borderRadius.resolve(Directionality.of(context))
        : BorderRadius.circular(10);
    Widget content = padding == null ? child : Padding(padding: padding!, child: child);
    if (onTap != null) {
      content = InkWell(borderRadius: radius, onTap: onTap, child: content);
    }
    return Card(
      color: color,
      margin: margin,
      clipBehavior: onTap == null ? Clip.none : Clip.antiAlias,
      shape: borderColor == null
          ? null
          : RoundedRectangleBorder(
              borderRadius: radius,
              side: BorderSide(color: borderColor!),
            ),
      child: content,
    );
  }
}

/// Section title in Cinzel with a gold rule on both sides.
class SectionHeader extends StatelessWidget {
  const SectionHeader(
    this.title, {
    super.key,
    this.padding = const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
    this.trailing,
  });

  final String title;
  final EdgeInsetsGeometry padding;

  /// Optional widget after the right rule (e.g. an action button).
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final rule = Expanded(child: Container(height: 1, color: tokens.gold.withValues(alpha: 0.6)));
    return Padding(
      padding: padding,
      child: Row(
        children: [
          rule,
          Flexible(
            flex: 0,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Text(
                title,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          ),
          rule,
          if (trailing != null) ...[const SizedBox(width: 8), trailing!],
        ],
      ),
    );
  }
}

/// Thin rule with a small gold diamond in the middle.
class RuneDivider extends StatelessWidget {
  const RuneDivider({super.key, this.padding = const EdgeInsets.symmetric(vertical: 8)});

  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final rule = Expanded(child: Container(height: 1, color: tokens.rune.withValues(alpha: 0.6)));
    return Padding(
      padding: padding,
      child: Row(
        children: [
          rule,
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Transform.rotate(
              angle: 0.7853981633974483,
              child: Container(width: 7, height: 7, color: tokens.gold),
            ),
          ),
          rule,
        ],
      ),
    );
  }
}
