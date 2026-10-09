import 'package:flutter/material.dart';

import '../../features/characters/data/models.dart' show BreakdownPart, ValueBreakdown;
import '../../features/characters/domain/character_format.dart' show formatModifier;
import '../../features/characters/domain/class_theme.dart' show classThemes;
import '../theme/app_icon.dart';
import '../theme/icons.dart';
import '../theme/tokens.dart';

/// Icon of a breakdown part, by its `source`.
Widget breakdownSourceIcon(BreakdownPart part, {double size = 20, Color? color}) {
  switch (part.source) {
    case 'race':
    case 'subrace':
      return AppIcon(AppIcons.users, size: size, color: color);
    case 'item':
      return AppIcon(AppIcons.treasure, size: size, color: color);
    case 'proficiency':
    case 'expertise':
      return AppIcon(AppIcons.d20, size: size, color: color);
    case 'armor':
    case 'shield':
      return AppIcon(AppIcons.shield, size: size, color: color);
    case 'override':
      return Icon(Icons.edit, size: size, color: color);
    case 'class':
    case 'feature':
      final label = part.label.toLowerCase();
      for (final theme in classThemes.values) {
        if (label.contains(theme.labelEs.toLowerCase())) {
          return AppIcon(theme.icon, size: size, color: color);
        }
      }
      return AppIcon(AppIcons.book, size: size, color: color);
    case 'ability':
      return Icon(Icons.bolt, size: size, color: color);
    case 'base':
      return Icon(Icons.tag, size: size, color: color);
    default:
      return AppIcon(AppIcons.book, size: size, color: color);
  }
}

/// Modal bottom sheet listing the parts of a [ValueBreakdown] (icon, label and
/// signed value) and the total.
///
/// [totalText] replaces the plain total, e.g. "30 pies" or "+5"; [lines]
/// explain the value before its parts ("Tabla del conjuro a nivel 3: 8d6").
Future<void> showBreakdownSheet(
  BuildContext context, {
  required String title,
  required ValueBreakdown breakdown,
  String? totalText,
  List<String> lines = const [],
}) {
  return showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    isScrollControlled: true,
    builder: (_) =>
        BreakdownSheet(title: title, breakdown: breakdown, totalText: totalText, lines: lines),
  );
}

/// Content of the breakdown sheet.
class BreakdownSheet extends StatelessWidget {
  const BreakdownSheet({
    super.key,
    required this.title,
    required this.breakdown,
    this.totalText,
    this.lines = const [],
  });

  final String title;
  final ValueBreakdown breakdown;
  final String? totalText;

  /// Explanations shown before the parts, keyed `breakdown-line-<i>`.
  final List<String> lines;

  /// The first `base` part reads "10", the rest "+3" / "-1".
  static String partText(BreakdownPart part, {required bool first}) =>
      first && part.source == 'base' && part.value >= 0
      ? '${part.value}'
      : formatModifier(part.value);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
        child: SingleChildScrollView(
          key: const Key('breakdown-sheet'),
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(title, key: const Key('breakdown-title'), style: theme.textTheme.titleLarge),
              const SizedBox(height: 8),
              for (var i = 0; i < lines.length; i++)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Text(
                    lines[i],
                    key: Key('breakdown-line-$i'),
                    style: theme.textTheme.bodyMedium,
                  ),
                ),
              if (breakdown.parts.isEmpty && lines.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Text('Sin desglose disponible.', style: theme.textTheme.bodyMedium),
                ),
              for (var i = 0; i < breakdown.parts.length; i++)
                Padding(
                  key: Key('breakdown-part-$i'),
                  padding: const EdgeInsets.symmetric(vertical: 6),
                  child: Row(
                    children: [
                      breakdownSourceIcon(
                        breakdown.parts[i],
                        color:
                            breakdown.parts[i].source == 'item' ||
                                breakdown.parts[i].source == 'override'
                            ? tokens.gold
                            : tokens.inkMuted,
                      ),
                      const SizedBox(width: 12),
                      Expanded(child: Text(breakdown.parts[i].label)),
                      Text(
                        partText(breakdown.parts[i], first: i == 0),
                        style: theme.textTheme.titleMedium,
                      ),
                    ],
                  ),
                ),
              Divider(color: tokens.gold.withValues(alpha: 0.6)),
              Row(
                children: [
                  Expanded(child: Text('Total', style: theme.textTheme.titleMedium)),
                  Text(
                    totalText ?? '${breakdown.total}',
                    key: const Key('breakdown-total'),
                    style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A value of the sheet ("+5", "17", "30 pies") that opens its
/// [BreakdownSheet] with one tap. A small gold dot marks values that an item
/// or a manual override contributes to.
///
/// Without a [breakdown] (older servers) it is plain text. The tappable part
/// carries `Key('stat-<statKey>')`, the dot `Key('stat-mark-<statKey>')`.
class StatValue extends StatelessWidget {
  const StatValue({
    super.key,
    required this.statKey,
    required this.text,
    required this.title,
    this.breakdown,
    this.style,
    this.totalText,
    this.textKey,
    this.lines = const [],
  });

  /// Identifies the value in keys, usually the breakdown key (`ability.dex`).
  final String statKey;

  /// What is shown ("+5", "17").
  final String text;

  /// Title of the sheet ("Destreza", "CA").
  final String title;
  final ValueBreakdown? breakdown;
  final TextStyle? style;

  /// Total line of the sheet when it differs from the plain number.
  final String? totalText;

  /// Key of the inner [Text], for callers that look the text up.
  final Key? textKey;

  /// Explanations the sheet shows before the parts (how dice were chosen).
  /// With lines and no [breakdown] the value still opens the sheet.
  final List<String> lines;

  @override
  Widget build(BuildContext context) {
    final b = breakdown ?? (lines.isEmpty ? null : const ValueBreakdown());
    if (b == null) return Text(text, key: textKey, style: style);
    final marked = b.hasItemOrOverride;
    return Semantics(
      button: true,
      hint: 'Ver desglose',
      child: InkWell(
        key: Key('stat-$statKey'),
        borderRadius: BorderRadius.circular(6),
        onTap: () => showBreakdownSheet(
          context,
          title: title,
          breakdown: b,
          totalText: totalText ?? text,
          lines: lines,
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(text, key: textKey, style: style),
              if (marked)
                Padding(
                  padding: const EdgeInsets.only(left: 2, top: 2),
                  child: DecoratedBox(
                    key: Key('stat-mark-$statKey'),
                    decoration: BoxDecoration(color: context.tokens.gold, shape: BoxShape.circle),
                    child: const SizedBox.square(dimension: 6),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
