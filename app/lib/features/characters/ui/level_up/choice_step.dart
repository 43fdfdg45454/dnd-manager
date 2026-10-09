import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/components.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../catalog/domain/catalog_format.dart' show abilityLabel, spellLevelLabel;
import '../../data/level_up_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart' show abilityAbbreviation;
import 'level_up_widgets.dart';

/// Lists with more options than this get a search field.
const _searchThreshold = 8;

/// Page of one choice: option cards (with prerequisites, eligibility and
/// effects), the "Mejora" / "Dote" tabs of an Ability Score Improvement, text
/// fields for free-text choices and, when allowed, the replacement of a known
/// pick at the end.
class LevelUpChoiceStep extends ConsumerStatefulWidget {
  const LevelUpChoiceStep({super.key, required this.characterId, required this.choiceKey});

  final String characterId;
  final String choiceKey;

  @override
  ConsumerState<LevelUpChoiceStep> createState() => _LevelUpChoiceStepState();
}

class _LevelUpChoiceStepState extends ConsumerState<LevelUpChoiceStep> {
  String _query = '';

  /// Spell level filter; null shows every level.
  int? _level;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(levelUpControllerProvider(widget.characterId));
    final controller = ref.read(levelUpControllerProvider(widget.characterId).notifier);
    final choice = state.choiceOf(widget.choiceKey);
    if (choice == null) return const SizedBox.shrink();
    final selection = state.selectionOf(choice.key);
    final needed = state.neededPicks(choice);
    final theme = Theme.of(context);
    final isImprovement = choice.kind == LevelChoiceKind.asiOrFeat;

    return LevelUpStepList(
      children: [
        LevelUpHeading(
          choice.name,
          subtitle: choice.note.isNotEmpty
              ? choice.note
              : choice.replacementOnly
              ? 'Puedes sustituir una elección anterior (opcional).'
              : null,
          warning: choice.warning,
          trailing: isImprovement
              ? null
              : Padding(
                  padding: const EdgeInsets.only(left: 8, top: 4),
                  child: Text(
                    '${choice.freeText ? selection.selected.where((s) => s.trim().isNotEmpty).length : selection.selected.length} de $needed',
                    key: Key('levelup-count-${choice.key}'),
                    style: theme.textTheme.titleMedium?.merge(AppTypography.numeric),
                  ),
                ),
        ),
        if (isImprovement && choice.isReplacement)
          _FeatPanel(state: state, choice: choice, selection: selection, controller: controller)
        else if (isImprovement)
          _ImprovementPicker(characterId: widget.characterId, choice: choice)
        else if (choice.freeText)
          for (var i = 0; i < needed; i++)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: TextFormField(
                key: Key('levelup-text-${choice.key}-$i'),
                initialValue: i < selection.selected.length ? selection.selected[i] : '',
                decoration: InputDecoration(
                  labelText: switch (choice.kind) {
                    LevelChoiceKind.language => 'Idioma ${i + 1}',
                    LevelChoiceKind.tool => 'Herramienta ${i + 1}',
                    _ => 'Elección ${i + 1}',
                  },
                ),
                onChanged: (value) => controller.setText(choice, i, value),
              ),
            )
        else
          ..._options(context, state, choice, selection, controller),
        if (choice.replaces && choice.known.isNotEmpty && !choice.isReplacement) ...[
          const SectionHeader(
            'Sustituir uno conocido',
            padding: EdgeInsets.symmetric(vertical: 12),
          ),
          Text(
            'Opcional: cambia una elección anterior por otra nueva (eliges una más).',
            style: theme.textTheme.bodySmall,
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final known in choice.known)
                ChoiceChip(
                  key: Key('levelup-replace-${choice.key}-${known.index}'),
                  label: Text(known.name),
                  selected: selection.replaced == known.index,
                  onSelected: (on) => controller.setReplaced(choice, on ? known.index : null),
                ),
            ],
          ),
        ],
      ],
    );
  }

  List<Widget> _options(
    BuildContext context,
    LevelUpState state,
    LevelUpChoice choice,
    LevelUpSelection selection,
    LevelUpController controller,
  ) {
    if (state.availableOptions(choice).isEmpty) {
      return [
        Text(
          choice.replacementOnly
              ? 'No hay nada nuevo que elegir en este nivel.'
              : 'No hay opciones disponibles.',
        ),
      ];
    }
    final spells = choice.kind.isSpells;
    final searchable = spells || choice.options.length > _searchThreshold;
    final levels = spells
        ? ({for (final o in choice.options) ?o.spellLevel}.toList()..sort())
        : const <int>[];
    final query = _query.trim().toLowerCase();
    final visible = [
      for (final o in state.availableOptions(choice))
        if ((query.isEmpty || o.name.toLowerCase().contains(query)) &&
            (_level == null || o.spellLevel == _level))
          o,
    ];
    return [
      if (searchable)
        Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: TextField(
            key: Key('levelup-search-${choice.key}'),
            decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Buscar'),
            onChanged: (value) => setState(() => _query = value),
          ),
        ),
      if (levels.length > 1)
        Wrap(
          spacing: 8,
          runSpacing: 4,
          children: [
            ChoiceChip(
              key: const Key('levelup-level-all'),
              label: const Text('Todos'),
              selected: _level == null,
              onSelected: (_) => setState(() => _level = null),
            ),
            for (final level in levels)
              ChoiceChip(
                key: Key('levelup-level-$level'),
                label: Text(spellLevelLabel(level)),
                selected: _level == level,
                onSelected: (on) => setState(() => _level = on ? level : null),
              ),
          ],
        ),
      if (visible.isEmpty)
        const Padding(padding: EdgeInsets.all(16), child: Text('Ninguna opción coincide.')),
      for (final option in visible)
        LevelUpOptionCard(
          key: Key('levelup-option-${choice.key}-${option.index}'),
          option: option,
          showSpellLevel: spells,
          selected: selection.selected.contains(option.index),
          onTap: () => controller.toggleOption(choice, option.index),
        ),
    ];
  }
}

/// The two tabs of an `AsiOrFeat` choice: "Mejora" (+2 to one ability or +1
/// to two, never above 20, shown live) and "Dote" (feats with eligibility and,
/// when the feat raises an ability of several, the ability picker).
class _ImprovementPicker extends ConsumerWidget {
  const _ImprovementPicker({required this.characterId, required this.choice});

  final String characterId;
  final LevelUpChoice choice;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(levelUpControllerProvider(characterId));
    final controller = ref.read(levelUpControllerProvider(characterId).notifier);
    final selection = state.selectionOf(choice.key);
    return DefaultTabController(
      length: 2,
      initialIndex: selection.mode.index,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            onTap: (i) => controller.setImprovementMode(choice, ImprovementMode.values[i]),
            tabs: const [
              Tab(key: Key('levelup-tab-asi'), text: 'Mejora'),
              Tab(key: Key('levelup-tab-feat'), text: 'Dote'),
            ],
          ),
          const SizedBox(height: 12),
          if (selection.mode == ImprovementMode.asi)
            _AsiPanel(state: state, choice: choice, selection: selection, controller: controller)
          else
            _FeatPanel(state: state, choice: choice, selection: selection, controller: controller),
        ],
      ),
    );
  }
}

class _AsiPanel extends StatelessWidget {
  const _AsiPanel({
    required this.state,
    required this.choice,
    required this.selection,
    required this.controller,
  });

  final LevelUpState state;
  final LevelUpChoice choice;
  final LevelUpSelection selection;
  final LevelUpController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final total = selection.asiTotal;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '+2 a una característica o +1 a dos (máximo $improvementMaxScore).',
          style: theme.textTheme.bodySmall,
        ),
        const SizedBox(height: 4),
        Text(
          'Puntos repartidos: $total de $improvementPoints',
          key: const Key('levelup-asi-total'),
          style: theme.textTheme.titleSmall,
        ),
        const SizedBox(height: 8),
        for (final ability in abilityKeys)
          Builder(
            builder: (context) {
              final points = selection.asi[ability] ?? 0;
              final score = state.naturalScore(ability);
              final atCap = score + points >= improvementMaxScore;
              final canAdd = total < improvementPoints && points < improvementPoints && !atCap;
              return Padding(
                padding: const EdgeInsets.symmetric(vertical: 2),
                child: Row(
                  children: [
                    Expanded(child: Text(abilityLabel(ability))),
                    Text(
                      points > 0 ? '$score → ${score + points}' : '$score',
                      key: Key('levelup-asi-score-$ability'),
                      style: theme.textTheme.titleMedium
                          ?.merge(AppTypography.numeric)
                          .copyWith(color: points > 0 ? tokens.moss : null),
                    ),
                    if (atCap)
                      Padding(
                        padding: const EdgeInsets.only(left: 6),
                        child: Text(
                          'máx. $improvementMaxScore',
                          key: Key('levelup-asi-cap-$ability'),
                          style: theme.textTheme.labelSmall?.copyWith(color: tokens.oldGold),
                        ),
                      ),
                    IconButton(
                      key: Key('levelup-asi-$ability-minus'),
                      tooltip: 'Quitar un punto',
                      onPressed: points > 0
                          ? () => controller.adjustAsi(choice, ability, -1)
                          : null,
                      icon: const Icon(Icons.remove_circle_outline),
                    ),
                    SizedBox(
                      width: 24,
                      child: Text(
                        '+$points',
                        textAlign: TextAlign.center,
                        style: AppTypography.numeric,
                      ),
                    ),
                    IconButton(
                      key: Key('levelup-asi-$ability-plus'),
                      tooltip: 'Añadir un punto',
                      onPressed: canAdd ? () => controller.adjustAsi(choice, ability, 1) : null,
                      icon: const Icon(Icons.add_circle_outline),
                    ),
                  ],
                ),
              );
            },
          ),
      ],
    );
  }
}

class _FeatPanel extends StatelessWidget {
  const _FeatPanel({
    required this.state,
    required this.choice,
    required this.selection,
    required this.controller,
  });

  final LevelUpState state;
  final LevelUpChoice choice;
  final LevelUpSelection selection;
  final LevelUpController controller;

  @override
  Widget build(BuildContext context) {
    if (choice.options.isEmpty) {
      return const Text('No hay dotes disponibles en esta partida.');
    }
    final feat = selection.feat == null ? null : choice.option(selection.feat!);
    final increase = feat?.abilityIncrease;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final option in choice.options)
          LevelUpOptionCard(
            key: Key('levelup-option-${choice.key}-${option.index}'),
            option: option,
            selected: selection.feat == option.index,
            onTap: () => controller.selectFeat(choice, option.index),
          ),
        if (feat != null && increase != null && increase.needsPick) ...[
          const SizedBox(height: 8),
          Text(
            '${feat.name} sube +${increase.amount} a:',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 4),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final ability in increase.options)
                ChoiceChip(
                  key: Key('levelup-feat-ability-$ability'),
                  label: Text(
                    '${abilityAbbreviation(ability)} ${state.naturalScore(ability)} → '
                    '${state.naturalScore(ability) + increase.amount}',
                  ),
                  selected: selection.featAbility == ability,
                  onSelected: state.naturalScore(ability) + increase.amount > improvementMaxScore
                      ? null
                      : (_) => controller.setFeatAbility(choice, ability),
                ),
            ],
          ),
        ],
      ],
    );
  }
}
