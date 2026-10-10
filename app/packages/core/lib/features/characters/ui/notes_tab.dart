import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/characters/models.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/detail_widgets.dart';

/// "Notas" of a character: the facts of its game system on top
/// ([GameSystemUi.notesFacts]; D&D 5e: background and alignment), then the
/// personality, the notes and the backstory.
class NotesTab extends ConsumerWidget {
  const NotesTab({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final facts = ref.watch(campaignSystemUiProvider(c.campaignId)).notesFacts(c);
    return ListView(
      key: const Key('sheet-tab-list'),
      padding: EdgeInsets.fromLTRB(16, 8, 16, 88 + MediaQuery.paddingOf(context).bottom),
      children: [
        ?facts,
        const SectionTitle('Personalidad'),
        if (!c.hasPersonality)
          const Text('Sin personalidad.', key: Key('sheet-personality-empty'))
        else
          Column(
            key: const Key('sheet-personality'),
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _PersonalityRow('Rasgos de personalidad', c.personalityTraits),
              _PersonalityRow('Ideal', c.ideals),
              _PersonalityRow('Vínculo', c.bonds),
              _PersonalityRow('Defecto', c.flaws),
              _PersonalityRow('Detalle del trasfondo', c.backgroundDetail),
            ],
          ),
        const SectionTitle('Notas'),
        c.notes.trim().isEmpty ? const Text('Sin notas.') : SelectableText(c.notes),
        const SectionTitle('Historia del personaje'),
        c.backstory.trim().isEmpty ? const Text('Sin historia.') : SelectableText(c.backstory),
      ],
    );
  }
}

/// A labelled personality text; nothing when [text] is empty.
class _PersonalityRow extends StatelessWidget {
  const _PersonalityRow(this.label, this.text);

  final String label;
  final String text;

  @override
  Widget build(BuildContext context) {
    if (text.trim().isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.labelMedium),
          SelectableText(text.trim()),
        ],
      ),
    );
  }
}
