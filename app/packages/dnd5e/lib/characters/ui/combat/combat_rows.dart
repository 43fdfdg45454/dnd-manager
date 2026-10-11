import 'package:flutter/material.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/components.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/textures.dart';

import '../../../ui/action_type.dart';
import 'combat_support.dart';

/// One row of the combat view: an icon, the name with what it costs on the
/// turn ([actionKind]) and what it spends ([cost]), one line saying what it
/// does ([subtitle]), the info button ([info]) and the action on the right
/// ([action]). [details] go below, aligned with the text (rolls, toggles,
/// facts); [actions] is a wrap of extra buttons aligned to the right.
class CombatActionRow extends StatelessWidget {
  const CombatActionRow({
    super.key,
    required this.title,
    this.icon,
    this.actionKind,
    this.actionLabel,
    this.cost,
    this.costKey,
    this.badge,
    this.subtitle,
    this.subtitleKey,
    this.info,
    this.action,
    this.actions = const [],
    this.details = const [],
  });

  final String title;
  final AppIcons? icon;

  /// What using it costs on the turn; null hides the chip.
  final ActionKind? actionKind;

  /// Text of the chip instead of the kind's label ("10 minutos").
  final String? actionLabel;

  /// What it spends or has left ("1 ki", "2 / 3", "1/descanso largo").
  final String? cost;
  final Key? costKey;

  /// A value next to the name (the attack bonus with its breakdown).
  final Widget? badge;
  final String? subtitle;
  final Key? subtitleKey;

  /// Usually a [DetailInfoButton] or a [FeatureInfoButton].
  final Widget? info;

  /// The main button, on the right.
  final Widget? action;

  /// More buttons, below and aligned to the right.
  final List<Widget> actions;
  final List<Widget> details;

  /// Widest share of the row the [action] may take.
  static const actionShare = 0.45;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant);
    final indent = icon == null ? 0.0 : 32.0;
    final kind = actionKind;
    final heading = Wrap(
      spacing: 8,
      runSpacing: 2,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(title, style: theme.textTheme.titleSmall),
        if (kind != null) ActionTypeChip(kind, label: actionLabel, compact: true),
        if (cost != null)
          Text(cost!, key: costKey, style: numericStyle(theme.textTheme.labelMedium)),
        ?badge,
      ],
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: LayoutBuilder(
        builder: (context, constraints) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                if (icon != null)
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: AppIcon(icon!, size: 24, color: theme.colorScheme.primary),
                  ),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      heading,
                      if (subtitle != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 2),
                          child: Text(subtitle!, key: subtitleKey, style: muted),
                        ),
                    ],
                  ),
                ),
                ?info,
                if (action != null)
                  Padding(
                    padding: const EdgeInsets.only(left: 4),
                    child: ConstrainedBox(
                      constraints: BoxConstraints(maxWidth: constraints.maxWidth * actionShare),
                      child: action,
                    ),
                  ),
              ],
            ),
            if (details.isNotEmpty)
              Padding(
                padding: EdgeInsets.only(left: indent, top: 4),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: details),
              ),
            if (actions.isNotEmpty)
              Padding(
                padding: EdgeInsets.only(left: indent, top: 6),
                child: Align(
                  alignment: AlignmentDirectional.centerEnd,
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    alignment: WrapAlignment.end,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: actions,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// A titled group of [CombatActionRow]s in one rune card, separated by thin
/// rules. [leading] goes before the title (the class icon); [trailing] after
/// the rule. Nothing is drawn when [children] is empty.
class CombatGroup extends StatelessWidget {
  const CombatGroup({
    super.key,
    required this.title,
    required this.children,
    this.leading,
    this.trailing,
  });

  final String title;
  final Widget? leading;
  final Widget? trailing;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    if (children.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (leading == null)
          SectionHeader(title, padding: combatSectionPadding, trailing: trailing)
        else
          Padding(
            padding: combatSectionPadding,
            child: Row(
              children: [
                leading!,
                const SizedBox(width: 8),
                Expanded(child: Text(title, style: theme.textTheme.titleMedium)),
                ?trailing,
              ],
            ),
          ),
        RuneCard(
          margin: const EdgeInsets.symmetric(vertical: 4),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          seed: title.hashCode,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (var i = 0; i < children.length; i++) ...[
                if (i > 0) const Divider(height: 1),
                children[i],
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// Bottom sheet with the rules text of something the catalog has no page for
/// (an option, a feat, a racial trait of a content pack).
Future<void> showRulesTextSheet(BuildContext context, String title, String text) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: SingleChildScrollView(
          key: const Key('rules-text-sheet'),
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(sheetContext).textTheme.titleLarge),
              const SizedBox(height: 8),
              for (final paragraph in text.split('\n\n'))
                Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(paragraph)),
            ],
          ),
        ),
      ),
    );
