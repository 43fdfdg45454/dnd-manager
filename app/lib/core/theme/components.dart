import 'package:flutter/material.dart';

import 'textures.dart';
import 'tokens.dart';
import 'typography.dart';

/// Component themes built from the [AppTokens] of one brightness.
class AppComponentThemes {
  AppComponentThemes(this.tokens, this.scheme, this.textTheme);

  final AppTokens tokens;
  final ColorScheme scheme;
  final TextTheme textTheme;

  bool get _dark => scheme.brightness == Brightness.dark;

  Color get _goldBorder => tokens.oldGold.withValues(alpha: 0.45);

  static const _radius = 6.0;

  /// Cut ("carved") corners for cards and dialogs.
  static ShapeBorder _carved(double radius, BorderSide side) =>
      BeveledRectangleBorder(borderRadius: BorderRadius.circular(radius), side: side);

  CardThemeData get card => CardThemeData(
    color: tokens.stone,
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
    backgroundColor: tokens.stoneRaised,
    foregroundColor: tokens.bone,
    surfaceTintColor: Colors.transparent,
    scrolledUnderElevation: 0,
    titleTextStyle: textTheme.titleLarge?.copyWith(fontSize: 21, color: tokens.bone),
    shape: Border(bottom: BorderSide(color: _goldBorder)),
  );

  FilledButtonThemeData get filledButton => FilledButtonThemeData(
    style: FilledButton.styleFrom(
      backgroundColor: tokens.ember,
      foregroundColor: scheme.onPrimary,
      textStyle: AppTypography.sans(weight: FontWeight.w600),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
    ),
  );

  OutlinedButtonThemeData get outlinedButton => OutlinedButtonThemeData(
    style: OutlinedButton.styleFrom(
      foregroundColor: tokens.bone,
      side: BorderSide(color: tokens.oldGold),
      textStyle: AppTypography.sans(weight: FontWeight.w600),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
    ),
  );

  TextButtonThemeData get textButton => TextButtonThemeData(
    // Ember reads well on obsidian; on bone it is too light for text.
    style: TextButton.styleFrom(
      foregroundColor: _dark ? tokens.ember : tokens.blood,
      textStyle: AppTypography.sans(weight: FontWeight.w600),
    ),
  );

  FloatingActionButtonThemeData get floatingActionButton => FloatingActionButtonThemeData(
    backgroundColor: tokens.ember,
    foregroundColor: scheme.onPrimary,
    shape: _carved(10, BorderSide.none),
  );

  TabBarThemeData get tabBar => TabBarThemeData(
    indicator: UnderlineTabIndicator(borderSide: BorderSide(color: tokens.oldGold, width: 3)),
    indicatorSize: TabBarIndicatorSize.tab,
    labelColor: tokens.bone,
    unselectedLabelColor: tokens.boneMuted,
    labelStyle: const TextStyle(fontFamily: AppFonts.display, fontWeight: FontWeight.w700),
    unselectedLabelStyle: const TextStyle(
      fontFamily: AppFonts.display,
      fontWeight: FontWeight.w400,
    ),
    dividerColor: tokens.rune.withValues(alpha: 0.7),
  );

  ChipThemeData get chip => ChipThemeData(
    side: BorderSide(color: tokens.rune),
    labelStyle: textTheme.labelLarge,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
  );

  NavigationBarThemeData get navigationBar => NavigationBarThemeData(
    backgroundColor: tokens.stoneRaised,
    surfaceTintColor: Colors.transparent,
    indicatorColor: tokens.oldGold.withValues(alpha: 0.3),
    labelTextStyle: WidgetStatePropertyAll(
      AppTypography.sans(weight: FontWeight.w600, color: tokens.bone),
    ),
  );

  SegmentedButtonThemeData get segmentedButton => SegmentedButtonThemeData(
    style: SegmentedButton.styleFrom(
      foregroundColor: tokens.bone,
      selectedForegroundColor: tokens.bone,
      selectedBackgroundColor: tokens.oldGold.withValues(alpha: 0.3),
      side: BorderSide(color: tokens.rune),
      textStyle: AppTypography.sans(weight: FontWeight.w600),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
    ),
  );

  ProgressIndicatorThemeData get progress => ProgressIndicatorThemeData(
    color: tokens.ember,
    linearTrackColor: tokens.stoneRaised,
    circularTrackColor: tokens.stoneRaised,
  );

  DividerThemeData get divider => DividerThemeData(color: tokens.rune, thickness: 1, space: 1);

  SnackBarThemeData get snackBar => SnackBarThemeData(
    backgroundColor: tokens.bone,
    contentTextStyle: AppTypography.sans(fontSize: 15, color: tokens.obsidian),
    actionTextColor: _dark ? tokens.blood : tokens.oldGold,
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
  );

  DialogThemeData get dialog => DialogThemeData(
    backgroundColor: tokens.stone,
    surfaceTintColor: Colors.transparent,
    titleTextStyle: textTheme.titleLarge?.copyWith(color: tokens.bone),
    shape: _carved(10, BorderSide(color: _goldBorder)),
  );

  BottomSheetThemeData get bottomSheet => BottomSheetThemeData(
    backgroundColor: tokens.stone,
    surfaceTintColor: Colors.transparent,
    modalBackgroundColor: tokens.stone,
  );

  PopupMenuThemeData get popupMenu => PopupMenuThemeData(
    color: tokens.stoneRaised,
    surfaceTintColor: Colors.transparent,
    textStyle: textTheme.bodyLarge,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(4),
      side: BorderSide(color: _goldBorder),
    ),
  );
}

/// Default surface for content: a [RuneCard] (stone fill, old-gold brush
/// border). Kept under its previous name so existing screens adopt the skin.
class ParchmentCard extends StatelessWidget {
  const ParchmentCard({super.key, required this.child, this.margin, this.padding, this.onTap});

  final Widget child;

  /// Defaults to the card theme's margin.
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) =>
      RuneCard(margin: margin, padding: padding, onTap: onTap, seed: key?.hashCode, child: child);
}

/// Raised card for secondary content: [AppTokens.stoneRaised] fill with a
/// rune-coloured brush border.
class StoneCard extends StatelessWidget {
  const StoneCard({super.key, required this.child, this.margin, this.padding, this.onTap});

  final Widget child;
  final EdgeInsetsGeometry? margin;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return RuneCard(
      color: tokens.stoneRaised,
      borderColor: tokens.rune,
      margin: margin,
      padding: padding,
      onTap: onTap,
      seed: key?.hashCode,
      child: child,
    );
  }
}

/// Section title in Almendra with a gold rule on both sides.
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
    final rule = Expanded(
      child: Container(height: 1, color: tokens.oldGold.withValues(alpha: 0.6)),
    );
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
              child: Container(width: 7, height: 7, color: tokens.oldGold),
            ),
          ),
          rule,
        ],
      ),
    );
  }
}
