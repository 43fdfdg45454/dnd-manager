import 'package:flutter/material.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';
import 'package:opentrpg_core/core/theme/typography.dart';

import '../data/character_wizard_controller.dart' show OriginAnswer;
import '../domain/character_format.dart' show abilityAbbreviation, damageTypeLabel;
import '../models.dart';
import 'level_up/level_up_widgets.dart';

/// Lists with more options than this get a search field.
const _searchThreshold = 8;

/// Spanish name of the origin of a choice.
String originSourceLabel(String source) => switch (source) {
  'subrace' => 'Subraza',
  'background' => 'Trasfondo',
  'campaign' => 'Campaña',
  classChoiceSource => 'Clase · nivel 1',
  _ => 'Raza',
};

/// Label of the [slot]-th text field of a free-text choice.
String originTextLabel(OriginChoice choice, int slot) => switch (choice.kind) {
  OriginChoiceKind.language => 'Idioma ${slot + 1}',
  OriginChoiceKind.tool => 'Herramienta ${slot + 1}',
  _ => 'Valor ${slot + 1}',
};

/// One decision of the race, subrace or background with the same option cards
/// as the level-up choices ([LevelUpOptionCard]): a heading with the counter
/// and the note, then the options (or text fields for free-text choices, or the
/// feats with the ability they raise).
class OriginChoiceView extends StatefulWidget {
  const OriginChoiceView({
    super.key,
    required this.choice,
    required this.answer,
    required this.onToggle,
    required this.onText,
    required this.onFeat,
    required this.onFeatAbility,
    this.onRemovedSave,
    this.isAvailable,
  });

  final OriginChoice choice;
  final OriginAnswer answer;
  final ValueChanged<String> onToggle;
  final void Function(int slot, String text) onText;
  final ValueChanged<String> onFeat;
  final ValueChanged<String> onFeatAbility;

  /// Marks or unmarks a saving throw given up with the chosen feat; without
  /// it the picker is not shown.
  final ValueChanged<String>? onRemovedSave;

  /// Whether an option can be shown now: the expertise in a skill picked in
  /// another choice of the same step waits for that pick. All by default.
  final bool Function(LevelUpOption option)? isAvailable;

  @override
  State<OriginChoiceView> createState() => _OriginChoiceViewState();
}

class _OriginChoiceViewState extends State<OriginChoiceView> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final choice = widget.choice;
    final answer = widget.answer;
    final isFeat = choice.kind == OriginChoiceKind.feat;
    final count = isFeat
        ? (answer.feat == null ? 0 : 1)
        : answer.picks.where((p) => p.trim().isNotEmpty).length;
    final optional = choice.required == 0;
    final query = _query.trim().toLowerCase();
    final available = widget.isAvailable;
    final visible = [
      for (final o in choice.options)
        if ((query.isEmpty || o.name.toLowerCase().contains(query)) &&
            (available == null || available(o)))
          o,
    ];
    final feat = answer.feat == null ? null : choice.option(answer.feat!);
    final increase = feat?.abilityIncrease;
    final remove = feat?.removeSavingThrows;

    return Padding(
      key: Key('origin-choice-${choice.key}'),
      padding: const EdgeInsets.only(bottom: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(choice.name, style: theme.textTheme.titleMedium),
                    Text(
                      optional
                          ? '${originSourceLabel(choice.source)} · opcional'
                          : originSourceLabel(choice.source),
                      style: theme.textTheme.labelSmall?.copyWith(color: tokens.boneMuted),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(left: 8, top: 2),
                child: Text(
                  '$count de ${choice.choose}',
                  key: Key('origin-count-${choice.key}'),
                  style: theme.textTheme.titleMedium?.merge(AppTypography.numeric),
                ),
              ),
            ],
          ),
          if (choice.note.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                choice.note,
                style: theme.textTheme.bodySmall?.copyWith(color: tokens.boneMuted),
              ),
            ),
          if (choice.warning != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                choice.warning!,
                key: Key('origin-warning-${choice.key}'),
                style: theme.textTheme.bodySmall?.copyWith(color: tokens.boneMuted),
              ),
            ),
          const SizedBox(height: 8),
          if (choice.freeText)
            for (var i = 0; i < choice.choose; i++)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: TextFormField(
                  key: Key('origin-text-${choice.key}-$i'),
                  initialValue: i < answer.picks.length ? answer.picks[i] : '',
                  decoration: InputDecoration(labelText: originTextLabel(choice, i)),
                  onChanged: (value) => widget.onText(i, value),
                ),
              )
          else ...[
            if (choice.options.length > _searchThreshold)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: TextField(
                  key: Key('origin-search-${choice.key}'),
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    hintText: 'Buscar',
                  ),
                  onChanged: (value) => setState(() => _query = value),
                ),
              ),
            if (choice.options.isEmpty)
              const Text('No hay opciones disponibles.')
            else if (visible.isEmpty)
              const Padding(padding: EdgeInsets.all(16), child: Text('Ninguna opción coincide.')),
            for (final option in visible)
              LevelUpOptionCard(
                key: Key('origin-option-${choice.key}-${option.index}'),
                option: _forDisplay(option),
                showSpellLevel: choice.isClassChoice,
                selected: isFeat
                    ? answer.feat == option.index
                    : answer.picks.contains(option.index),
                onTap: () => isFeat ? widget.onFeat(option.index) : widget.onToggle(option.index),
              ),
          ],
          if (feat != null && increase != null && increase.needsPick) ...[
            const SizedBox(height: 8),
            Text('${feat.name} sube +${increase.amount} a:', style: theme.textTheme.titleSmall),
            const SizedBox(height: 4),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final ability in increase.options)
                  ChoiceChip(
                    key: Key('origin-feat-ability-$ability'),
                    label: Text(abilityAbbreviation(ability)),
                    selected: answer.ability == ability,
                    onSelected: (_) => widget.onFeatAbility(ability),
                  ),
              ],
            ),
          ],
          if (feat != null && remove != null && remove.asks && widget.onRemovedSave != null) ...[
            const SizedBox(height: 8),
            Text(
              remove.choose == 1
                  ? 'Con ${feat.name} pierdes la competencia en 1 salvación:'
                  : 'Con ${feat.name} pierdes la competencia en ${remove.choose} salvaciones:',
              key: const Key('origin-removed-saves-title'),
              style: theme.textTheme.titleSmall,
            ),
            const SizedBox(height: 4),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final ability in remove.from.isEmpty ? abilityKeys : remove.from)
                  FilterChip(
                    key: Key('origin-removed-save-$ability'),
                    label: Text(abilityAbbreviation(ability)),
                    selected: answer.removedSavingThrows.contains(ability),
                    onSelected: (_) => widget.onRemovedSave!(ability),
                  ),
              ],
            ),
          ],
          if (feat != null && feat.skillChoicesDelta != 0)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                feat.skillChoicesDelta < 0
                    ? '${feat.name}: eliges ${-feat.skillChoicesDelta} habilidad(es) de clase menos'
                    : '${feat.name}: eliges ${feat.skillChoicesDelta} habilidad(es) de clase más',
                key: const Key('origin-skill-delta'),
                style: theme.textTheme.bodySmall?.copyWith(color: tokens.boneMuted),
              ),
            ),
        ],
      ),
    );
  }

  /// The draconic ancestry options show the resistance they give as an effect;
  /// cantrip options always count as spells of level 0, so their card carries
  /// the [DetailInfoButton] that opens the spell (as in the level-up).
  LevelUpOption _forDisplay(LevelUpOption option) {
    final type = option.damageType;
    final cantrip = widget.choice.kind == OriginChoiceKind.cantrip && option.spellLevel == null;
    if (type == null && !cantrip) return option;
    return LevelUpOption(
      index: option.index,
      name: option.name,
      description: [
        if (type != null) 'Resistencia al daño ${damageTypeLabel(type)}.',
        ...option.description,
      ],
      prerequisitesText: option.prerequisitesText,
      eligible: option.eligible,
      reason: option.reason,
      spellLevel: cantrip ? 0 : option.spellLevel,
      spellCategory: option.spellCategory,
      effectsPreview: option.effectsPreview,
      abilityIncrease: option.abilityIncrease,
      damageType: type,
      removeSavingThrows: option.removeSavingThrows,
      skillChoicesDelta: option.skillChoicesDelta,
    );
  }
}
