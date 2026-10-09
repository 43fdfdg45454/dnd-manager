import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/ui/spell_category.dart';
import '../../../catalog/ui/catalog_detail_links.dart';

import '../../data/character_wizard_controller.dart';
import '../../data/models.dart';
import '../spell_picker_page.dart';
import 'step_basics.dart' show stepPadding;

/// Cantrips and level 1 spells of a casting class, with "n/max" counters.
class SpellsStep extends ConsumerWidget {
  const SpellsStep({super.key, required this.args});

  final WizardArgs args;

  Future<void> _pick(
    BuildContext context,
    WidgetRef ref,
    WizardState state, {
    required bool cantrips,
  }) async {
    final detail = state.classDetail!;
    final remaining = cantrips
        ? state.maxCantrips - state.cantrips.length
        : state.maxSpells - state.leveledSpells.length;
    final picked = await Navigator.of(context).push<List<CharacterSpell>>(
      MaterialPageRoute(
        builder: (_) => SpellPickerPage(
          classes: [(classIndex: detail.index, className: detail.name, level: 1)],
          chosen: {for (final s in state.spells) s.spellIndex},
          minLevel: cantrips ? 0 : 1,
          maxLevel: cantrips ? 0 : null,
          limit: remaining < 0 ? 0 : remaining,
          title: cantrips
              ? 'Elegir trucos'
              : state.hasSpellbook
              ? 'Elegir hechizos del libro'
              : 'Elegir hechizos',
        ),
      ),
    );
    if (picked == null || picked.isEmpty) return;
    ref.read(characterWizardControllerProvider(args).notifier).addSpells(picked);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final theme = Theme.of(context);

    Widget section({
      required String title,
      required int max,
      required List<CharacterSpell> chosen,
      required bool cantrips,
      String? hint,
    }) {
      final over = chosen.length > max;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '$title ${chosen.length}/$max',
            key: Key(cantrips ? 'wizard-cantrips-counter' : 'wizard-spells-counter'),
            style: theme.textTheme.titleMedium?.copyWith(
              color: over ? theme.colorScheme.error : null,
            ),
          ),
          if (hint != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Text(hint, style: theme.textTheme.bodySmall),
            ),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final s in chosen)
                InputChip(
                  key: Key('spell-${s.spellIndex}'),
                  avatar: SpellCategoryIcon(s.category, size: 16),
                  label: Text(s.name ?? s.spellIndex),
                  tooltip: 'Ver detalle',
                  onPressed: () => openSpellDetail(context, s.spellIndex),
                  onDeleted: () => controller.removeSpell(s.spellIndex),
                ),
            ],
          ),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              key: Key(cantrips ? 'wizard-pick-cantrips' : 'wizard-pick-spells'),
              onPressed: chosen.length >= max
                  ? null
                  : () => _pick(context, ref, state, cantrips: cantrips),
              icon: const Icon(Icons.add),
              label: Text(
                cantrips
                    ? 'Elegir trucos'
                    : state.hasSpellbook
                    ? 'Elegir hechizos del libro'
                    : 'Elegir hechizos',
              ),
            ),
          ),
        ],
      );
    }

    return ListView(
      key: const Key('step-spells'),
      padding: stepPadding,
      children: [
        if (state.maxCantrips > 0)
          section(title: 'Trucos', max: state.maxCantrips, chosen: state.cantrips, cantrips: true),
        if (state.maxSpells > 0) ...[
          const SizedBox(height: 16),
          section(
            title: state.hasSpellbook ? 'Libro de hechizos' : 'Hechizos',
            max: state.maxSpells,
            chosen: state.leveledSpells,
            cantrips: false,
            hint: state.hasSpellbook
                ? 'Copias estos hechizos a tu libro; de ellos preparas solo algunos.'
                : null,
          ),
        ],
        if (state.hasSpellbook) ...[
          const SizedBox(height: 16),
          Text(
            'Preparados ${state.preparedChosen.length}/${state.maxPrepared}',
            key: const Key('wizard-prepared-counter'),
            style: theme.textTheme.titleMedium?.copyWith(
              color: state.preparedChosen.length > state.maxPrepared
                  ? theme.colorScheme.error
                  : null,
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Text(
              'Nivel de mago + modificador de Inteligencia (mínimo 1).',
              style: theme.textTheme.bodySmall,
            ),
          ),
          if (state.leveledSpells.isEmpty)
            Text('Elige primero los hechizos del libro.', style: theme.textTheme.bodySmall),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final s in state.leveledSpells)
                FilterChip(
                  key: Key('prepare-${s.spellIndex}'),
                  avatar: SpellCategoryIcon(s.category, size: 16),
                  label: Text(s.name ?? s.spellIndex),
                  selected: state.preparedChosen.contains(s.spellIndex),
                  onSelected: (_) => controller.togglePrepared(s.spellIndex),
                ),
            ],
          ),
        ],
      ],
    );
  }
}
