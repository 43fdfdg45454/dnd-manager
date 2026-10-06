import 'package:flutter/material.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/textures.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/spell_category.dart';
import '../../../catalog/domain/catalog_format.dart' show spellLevelLabel;
import '../../data/models.dart';

/// Scrollable body of a wizard page with room for the bottom bar.
class LevelUpStepList extends StatelessWidget {
  const LevelUpStepList({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return ListView(padding: const EdgeInsets.fromLTRB(16, 8, 16, 32), children: children);
  }
}

/// Big title of a wizard page with an optional line below.
class LevelUpHeading extends StatelessWidget {
  const LevelUpHeading(this.title, {super.key, this.subtitle, this.trailing});

  final String title;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(top: 8, bottom: 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.headlineSmall),
                if (subtitle != null && subtitle!.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    subtitle!,
                    style: theme.textTheme.bodyMedium?.copyWith(color: context.tokens.boneMuted),
                  ),
                ],
              ],
            ),
          ),
          ?trailing,
        ],
      ),
    );
  }
}

/// Paragraphs collapsed to three lines with "Ver más" / "Ver menos".
class ExpandableText extends StatefulWidget {
  const ExpandableText(this.paragraphs, {super.key, this.collapsedLines = 3});

  final List<String> paragraphs;
  final int collapsedLines;

  @override
  State<ExpandableText> createState() => _ExpandableTextState();
}

class _ExpandableTextState extends State<ExpandableText> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final paragraphs = widget.paragraphs.where((p) => p.trim().isNotEmpty).toList();
    if (paragraphs.isEmpty) return const SizedBox.shrink();
    final style = Theme.of(context).textTheme.bodySmall;
    final text = paragraphs.join('\n\n');
    final long = text.length > 160 || paragraphs.length > 1;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          text,
          style: style,
          maxLines: _expanded ? null : widget.collapsedLines,
          overflow: _expanded ? TextOverflow.visible : TextOverflow.ellipsis,
        ),
        if (long)
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton(
              style: TextButton.styleFrom(
                padding: EdgeInsets.zero,
                minimumSize: const Size(0, 32),
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
              onPressed: () => setState(() => _expanded = !_expanded),
              child: Text(_expanded ? 'Ver menos' : 'Ver más'),
            ),
          ),
      ],
    );
  }
}

/// Chip with an effect of an option: "CA 16 → 17".
class EffectChip extends StatelessWidget {
  const EffectChip({super.key, required this.effect});

  final EffectPreview effect;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: tokens.moss.withValues(alpha: 0.18),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: tokens.moss.withValues(alpha: 0.6)),
      ),
      child: Text(effect.text, style: Theme.of(context).textTheme.labelMedium),
    );
  }
}

/// An option of a level choice: name, prerequisites, why it cannot be taken,
/// its effects and its description. Ineligible options are dimmed and inert.
class LevelUpOptionCard extends StatelessWidget {
  const LevelUpOptionCard({
    super.key,
    required this.option,
    required this.selected,
    required this.onTap,
    this.showSpellLevel = false,
  });

  final LevelUpOption option;
  final bool selected;
  final VoidCallback? onTap;
  final bool showSpellLevel;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final enabled = option.eligible && onTap != null;
    return Opacity(
      opacity: option.eligible ? 1 : 0.55,
      child: RuneCard(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.all(12),
        color: selected ? tokens.ember.withValues(alpha: 0.16) : null,
        borderColor: selected ? tokens.ember : null,
        onTap: enabled ? onTap : null,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  selected ? Icons.check_circle : Icons.radio_button_unchecked,
                  size: 20,
                  color: selected ? tokens.ember : tokens.boneMuted,
                ),
                const SizedBox(width: 8),
                if (SpellCategory.fromApi(option.spellCategory) != null) ...[
                  SpellCategoryIcon(option.spellCategory),
                  const SizedBox(width: 6),
                ],
                Expanded(child: Text(option.name, style: theme.textTheme.titleSmall)),
                if (showSpellLevel && option.spellLevel != null)
                  Text(
                    spellLevelLabel(option.spellLevel!),
                    style: theme.textTheme.labelSmall?.copyWith(color: tokens.arcane),
                  ),
              ],
            ),
            if (option.prerequisitesText != null) ...[
              const SizedBox(height: 4),
              Text(
                'Requisito: ${option.prerequisitesText}',
                style: theme.textTheme.bodySmall?.copyWith(fontStyle: FontStyle.italic),
              ),
            ],
            if (!option.eligible) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  AppIcon(AppIcons.chains, size: 14, color: tokens.blood),
                  const SizedBox(width: 4),
                  Expanded(
                    child: Text(
                      option.reason ?? 'No cumples los requisitos.',
                      key: Key('levelup-reason-${option.index}'),
                      style: theme.textTheme.bodySmall?.copyWith(color: tokens.blood),
                    ),
                  ),
                ],
              ),
            ],
            if (option.effectsPreview.isNotEmpty) ...[
              const SizedBox(height: 6),
              Wrap(
                spacing: 6,
                runSpacing: 4,
                children: [for (final effect in option.effectsPreview) EffectChip(effect: effect)],
              ),
            ],
            if (option.description.isNotEmpty) ...[
              const SizedBox(height: 6),
              ExpandableText(option.description),
            ],
          ],
        ),
      ),
    );
  }
}
