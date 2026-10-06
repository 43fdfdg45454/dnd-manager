import 'package:flutter/material.dart';

import '../../../../core/files/authenticated_image.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../characters/data/models.dart' show RestKind, classesLabel;
import '../../../characters/domain/character_format.dart';
import '../../../characters/domain/class_theme.dart';
import '../../../characters/ui/character_tabs.dart' show titleFromSpellIndex;
import '../../data/models.dart';

/// The party at a glance for the DM: one row per active character. Tapping a
/// row calls [onOpen]; its checkbox toggles it in
/// [selected] through [onToggle].
class PartyRoster extends StatelessWidget {
  const PartyRoster({
    super.key,
    required this.members,
    required this.selected,
    required this.onToggle,
    required this.onOpen,
    this.conditions = const [],
  });

  final List<PartyMember> members;
  final Set<String> selected;
  final ValueChanged<String> onToggle;
  final ValueChanged<PartyMember> onOpen;

  /// SRD conditions, for their names.
  final List<Condition> conditions;

  @override
  Widget build(BuildContext context) {
    if (members.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 16),
        child: Center(
          child: Text('No hay personajes activos en la campaña.', key: Key('party-empty')),
        ),
      );
    }
    final names = {for (final c in conditions) c.index: c.name};
    return Column(
      key: const Key('party-roster'),
      children: [
        for (final m in members)
          PartyMemberRow(
            member: m,
            selected: selected.contains(m.id),
            conditionNames: names,
            onToggle: () => onToggle(m.id),
            onOpen: () => onOpen(m),
          ),
      ],
    );
  }
}

/// One character of the [PartyRoster].
class PartyMemberRow extends StatelessWidget {
  const PartyMemberRow({
    super.key,
    required this.member,
    required this.selected,
    required this.onToggle,
    required this.onOpen,
    this.conditionNames = const {},
  });

  final PartyMember member;
  final bool selected;
  final VoidCallback onToggle;
  final VoidCallback onOpen;
  final Map<String, String> conditionNames;

  @override
  Widget build(BuildContext context) {
    final m = member;
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final classTheme = classThemeOf(mainClassIndexOf(m.classes));
    final accent = classTheme.accent(theme.brightness);
    final classes = m.classes.isEmpty ? 'Sin clase' : classesLabel(m.classes);
    final hpText = m.temporaryHitPoints > 0
        ? 'PG ${m.hitPointsCurrent} / ${m.hitPointsMax} (+${m.temporaryHitPoints} temp.)'
        : 'PG ${m.hitPointsCurrent} / ${m.hitPointsMax}';

    return StoneCard(
      key: Key('party-member-${m.id}'),
      margin: const EdgeInsets.symmetric(vertical: 4),
      onTap: onOpen,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(8, 8, 12, 10),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Checkbox(
              key: Key('party-select-${m.id}'),
              value: selected,
              onChanged: (_) => onToggle(),
            ),
            _Portrait(member: m, accent: accent),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          m.name,
                          style: theme.textTheme.titleMedium,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      if (m.concentratingOnSpellIndex != null)
                        Tooltip(
                          message:
                              'Concentración: ${titleFromSpellIndex(m.concentratingOnSpellIndex!)}',
                          child: AppIcon(
                            AppIcons.anchor,
                            key: Key('party-concentration-${m.id}'),
                            size: 20,
                            color: tokens.arcane,
                          ),
                        ),
                      if (m.inspiration)
                        Padding(
                          padding: const EdgeInsets.only(left: 4),
                          child: Tooltip(
                            message: 'Inspiración',
                            child: AppIcon(AppIcons.sparkles, size: 18, color: tokens.gold),
                          ),
                        ),
                    ],
                  ),
                  Row(
                    children: [
                      AppIcon(classTheme.icon, size: 16, color: accent),
                      const SizedBox(width: 4),
                      Flexible(
                        child: Text(
                          '$classes · Nivel ${m.level}',
                          style: theme.textTheme.bodySmall?.copyWith(color: accent),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  HpBar(member: m),
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          hpText,
                          key: Key('party-hp-${m.id}'),
                          style: theme.textTheme.labelLarge?.merge(AppTypography.numeric),
                        ),
                      ),
                      Text(
                        'CA ${m.armorClass} · Inic. ${formatModifier(m.initiative)} · '
                        'Perc. ${m.passivePerception}',
                        key: Key('party-stats-${m.id}'),
                        style: theme.textTheme.bodySmall?.merge(AppTypography.numeric),
                      ),
                    ],
                  ),
                  if (m.isDown)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        'Salvaciones de muerte: ${m.deathSaveSuccesses} éxitos · '
                        '${m.deathSaveFailures} fallos',
                        key: Key('party-death-${m.id}'),
                        style: theme.textTheme.bodySmall
                            ?.merge(AppTypography.numeric)
                            .copyWith(color: tokens.blood),
                      ),
                    ),
                  if (m.conditions.isNotEmpty ||
                      m.exhaustionLevel > 0 ||
                      m.pendingRest != null ||
                      m.spellPreparationPending ||
                      m.pendingLevelUpTo != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Wrap(
                        spacing: 4,
                        runSpacing: 4,
                        children: [
                          if (m.pendingLevelUpTo != null)
                            Tooltip(
                              message: 'Nivel concedido: sube a nivel ${m.pendingLevelUpTo}',
                              child: Chip(
                                key: Key('party-levelup-${m.id}'),
                                avatar: AppIcon(AppIcons.levelUp, size: 16, color: tokens.gold),
                                label: Text(
                                  '↑ ${m.pendingLevelUpTo}',
                                  style: AppTypography.numeric,
                                ),
                                visualDensity: VisualDensity.compact,
                                padding: EdgeInsets.zero,
                              ),
                            ),
                          if (m.spellPreparationPending)
                            Tooltip(
                              message: 'Preparando conjuros',
                              child: Chip(
                                key: Key('party-preparing-${m.id}'),
                                avatar: AppIcon(AppIcons.spellbook, size: 16, color: tokens.gold),
                                label: const Text('Prepara'),
                                visualDensity: VisualDensity.compact,
                                padding: EdgeInsets.zero,
                              ),
                            ),
                          if (m.pendingRest != null)
                            Tooltip(
                              message: 'Pide un ${m.pendingRest!.description}',
                              child: Chip(
                                key: Key('party-rest-${m.id}'),
                                avatar: AppIcon(
                                  m.pendingRest!.kind == RestKind.short
                                      ? AppIcons.campfire
                                      : AppIcons.moon,
                                  size: 16,
                                  color: tokens.gold,
                                ),
                                label: const Text('Zz'),
                                visualDensity: VisualDensity.compact,
                                padding: EdgeInsets.zero,
                              ),
                            ),
                          for (final c in m.conditions)
                            Chip(
                              key: Key('party-condition-${m.id}-${c.index}'),
                              avatar: AppIcon(AppIcons.chains, size: 16, color: tokens.boneMuted),
                              label: Text(conditionNames[c.index] ?? titleFromSpellIndex(c.index)),
                              visualDensity: VisualDensity.compact,
                              padding: EdgeInsets.zero,
                            ),
                          if (m.exhaustionLevel > 0)
                            Chip(
                              key: Key('party-exhaustion-${m.id}'),
                              label: Text('Agotamiento ${m.exhaustionLevel}'),
                              visualDensity: VisualDensity.compact,
                              padding: EdgeInsets.zero,
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Portrait extends StatelessWidget {
  const _Portrait({required this.member, required this.accent});

  final PartyMember member;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    const size = 44.0;
    final url = member.portraitUrl;
    final initial = member.name.isEmpty ? '?' : member.name[0].toUpperCase();
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: accent, width: 2),
      ),
      child: ClipOval(
        child: url == null
            ? ColoredBox(
                color: accent.withValues(alpha: 0.2),
                child: Center(
                  child: Text(initial, style: TextStyle(color: accent, fontSize: 18)),
                ),
              )
            : AuthenticatedImage(url: url, width: size, height: size, compact: true),
      ),
    );
  }
}

/// Hit point bar: current hit points coloured by how hurt the character is,
/// then the temporary ones in the arcane colour, then what is missing.
class HpBar extends StatelessWidget {
  const HpBar({super.key, required this.member});

  final PartyMember member;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final max = member.hitPointsMax < 1 ? 1 : member.hitPointsMax;
    final current = member.hitPointsCurrent.clamp(0, max);
    final temp = member.temporaryHitPoints < 0 ? 0 : member.temporaryHitPoints;
    final ratio = current / max;
    final color = ratio > 0.5 ? tokens.emerald : (ratio > 0.25 ? tokens.gold : tokens.crimson);
    return ClipRRect(
      key: Key('party-hp-bar-${member.id}'),
      borderRadius: BorderRadius.circular(4),
      child: SizedBox(
        height: 8,
        child: Row(
          children: [
            if (current > 0)
              Expanded(
                flex: current,
                child: ColoredBox(color: color),
              ),
            if (temp > 0)
              Expanded(
                flex: temp,
                child: ColoredBox(key: Key('party-temp-bar-${member.id}'), color: tokens.arcane),
              ),
            if (max - current > 0)
              Expanded(
                flex: max - current,
                child: ColoredBox(color: tokens.rune.withValues(alpha: 0.25)),
              ),
          ],
        ),
      ),
    );
  }
}
