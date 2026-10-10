import 'package:flutter/material.dart';

import 'breakdown.dart';
import '../theme/components.dart';
import '../theme/typography.dart';
import 'stat_value.dart';

/// Fixed grid of equal tiles: 3 columns, or [columnsWide] from 600 px wide.
/// It never scrolls on its own; every child gets the same box.
class StatTileGrid extends StatelessWidget {
  const StatTileGrid({super.key, required this.columnsWide, required this.children});

  final int columnsWide;
  final List<Widget> children;

  static const _wideBreakpoint = 600.0;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final wide = constraints.maxWidth >= _wideBreakpoint;
        return GridView.count(
          crossAxisCount: wide ? columnsWide : 3,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: wide && columnsWide > 3 ? 1.3 : 1.15,
          children: children,
        );
      },
    );
  }
}

/// A stat tile for a [StatTileGrid]: the label on top and the value below,
/// which opens its [breakdown] on tap when there is one.
class StatTile extends StatelessWidget {
  const StatTile({
    super.key,
    required this.statKey,
    required this.label,
    required this.value,
    this.breakdown,
    this.breakdownKey,
    this.totalText,
    this.mark,
    this.onTap,
    this.corner,
    this.tapKey,
  });

  /// The tile is keyed `tile-<statKey>`.
  final String statKey;
  final String label;
  final String value;
  final ValueBreakdown? breakdown;

  /// Key of the [StatValue] (`stat-<breakdownKey>`); [statKey] when null.
  final String? breakdownKey;

  /// Total line of the breakdown sheet when it differs from [value].
  final String? totalText;

  /// Shown right of the value, usually an `OverrideMark`.
  final Widget? mark;

  /// Tap on the tile outside the value's breakdown.
  final VoidCallback? onTap;

  /// Small widget at the top right corner; it never changes the tile's size.
  final Widget? corner;

  /// Key of a subtree wrapping the whole tile, for callers that tap it.
  final Key? tapKey;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final valueStyle =
        theme.textTheme.titleLarge?.merge(AppTypography.numeric) ?? AppTypography.numeric;
    Widget tile = StoneCard(
      key: Key('tile-$statKey'),
      margin: EdgeInsets.zero,
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            label,
            style: theme.textTheme.labelMedium,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                StatValue(
                  statKey: breakdownKey ?? statKey,
                  title: label,
                  text: value,
                  totalText: totalText,
                  breakdown: breakdown,
                  style: valueStyle,
                ),
                ?mark,
              ],
            ),
          ),
        ],
      ),
    );
    final cornerWidget = corner;
    if (cornerWidget != null) {
      tile = Stack(
        // Passes the grid's tight box through: the corner never resizes the tile.
        fit: StackFit.passthrough,
        children: [
          tile,
          Positioned(top: 0, right: 0, child: cornerWidget),
        ],
      );
    }
    return tapKey == null ? tile : KeyedSubtree(key: tapKey, child: tile);
  }
}
