import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// State of a [SelectionTile].
enum SelectionState {
  /// Can be picked.
  available,

  /// Picked by the player.
  selected,

  /// Already granted by something else (background, race…): shown as
  /// picked but cannot be changed.
  locked,

  /// The limit is reached: it cannot be picked until another is released.
  blocked,
}

/// One option of a [SelectionGrid].
class SelectionItem {
  const SelectionItem({
    required this.id,
    required this.label,
    this.caption,
    this.state = SelectionState.available,
    this.tileKey,
  });

  final String id;
  final String label;

  /// Short secondary text under the label ("DES", "Exótico", "Trasfondo").
  final String? caption;
  final SelectionState state;
  final Key? tileKey;
}

/// A tidy grid of equal tiles for picking a few options from a list (skills,
/// languages, tools…): every tile has the same size whatever the length of
/// its label, 2 columns on phones and more on wider screens.
class SelectionGrid extends StatelessWidget {
  const SelectionGrid({super.key, required this.items, required this.onToggle});

  final List<SelectionItem> items;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    final textScale = MediaQuery.textScalerOf(context).scale(1);
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth;
        final columns = width < 420
            ? 2
            : width < 700
            ? 3
            : 4;
        return GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          padding: EdgeInsets.zero,
          itemCount: items.length,
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: columns,
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            mainAxisExtent: 60 * textScale.clamp(1.0, 1.4),
          ),
          itemBuilder: (context, i) {
            final item = items[i];
            final canTap =
                item.state == SelectionState.available || item.state == SelectionState.selected;
            return SelectionTile(
              key: item.tileKey,
              label: item.label,
              caption: item.caption,
              state: item.state,
              onTap: canTap ? () => onToggle(item.id) : null,
            );
          },
        );
      },
    );
  }
}

/// A tile of [SelectionGrid]: a check mark, the label (up to two lines) and
/// an optional caption, with a clear look for each [SelectionState].
class SelectionTile extends StatelessWidget {
  const SelectionTile({
    super.key,
    required this.label,
    this.caption,
    required this.state,
    this.onTap,
  });

  final String label;
  final String? caption;
  final SelectionState state;

  /// Null when the tile cannot be changed (locked or blocked by the limit).
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final picked = state == SelectionState.selected || state == SelectionState.locked;
    final accent = state == SelectionState.locked ? tokens.oldGold : tokens.ember;
    final border = picked ? accent : tokens.rune;
    final background = picked ? accent.withValues(alpha: 0.14) : tokens.stone;
    final icon = switch (state) {
      SelectionState.selected => Icons.check_circle,
      SelectionState.locked => Icons.lock_outline,
      _ => Icons.radio_button_unchecked,
    };

    return Opacity(
      opacity: state == SelectionState.blocked ? 0.45 : 1,
      child: Material(
        color: background,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(10),
          side: BorderSide(color: border, width: picked ? 1.6 : 1),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 10),
            child: Row(
              children: [
                Icon(icon, size: 20, color: picked ? accent : tokens.boneMuted),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        label,
                        maxLines: caption == null ? 2 : 1,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodyMedium?.copyWith(
                          fontWeight: picked ? FontWeight.w600 : null,
                        ),
                      ),
                      if (caption != null)
                        Text(
                          caption!,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.labelSmall?.copyWith(color: tokens.boneMuted),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
