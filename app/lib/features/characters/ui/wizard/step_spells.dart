import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/ui/spell_category.dart';

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
          title: cantrips ? 'Elegir trucos' : 'Elegir hechizos',
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
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              for (final s in chosen)
                InputChip(
                  key: Key('spell-${s.spellIndex}'),
                  avatar: SpellCategoryIcon(s.category, size: 16),
                  label: Text(s.name ?? s.spellIndex),
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
              label: Text(cantrips ? 'Elegir trucos' : 'Elegir hechizos'),
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
            title: 'Hechizos',
            max: state.maxSpells,
            chosen: state.leveledSpells,
            cantrips: false,
          ),
        ],
      ],
    );
  }
}
