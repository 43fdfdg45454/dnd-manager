import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/data/catalog_repository.dart';
import '../../../catalog/data/models.dart' show SpellDetail, SpellSummary;
import '../../../catalog/domain/catalog_format.dart' show spellLevelLabel;
import '../../dnd5e_characters_controller.dart';
import '../../domain/character_format.dart' show foldForSort;
import '../../domain/spell_combat.dart' show isCastable;
import '../../models.dart';

/// Concentration spells of the catalog matching a search, in the scope of a
/// campaign (its content packs).
final _concentrationSearchProvider = FutureProvider.autoDispose
    .family<List<SpellSummary>, ({String? campaignId, String search})>((ref, key) async {
      final page = await ref
          .watch(catalogRepositoryProvider)
          .spells(
            search: key.search.isEmpty ? null : key.search,
            concentration: true,
            campaignId: key.campaignId,
          );
      return [
        for (final s in page.items)
          if (s.concentration) s,
      ];
    });

/// Opens the [ConcentrationSpellPicker] of [character] and returns the spell
/// picked to concentrate on, or null.
Future<SpellSummary?> showConcentrationSpellPicker(
  BuildContext context,
  CharacterDetail character,
) {
  return showModalBottomSheet<SpellSummary>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.7,
      minChildSize: 0.4,
      maxChildSize: 0.95,
      builder: (_, controller) =>
          ConcentrationSpellPicker(character: character, scrollController: controller),
    ),
  );
}

/// Picks the spell a character concentrates on: concentration is always on a
/// spell. Offers the concentration spells the character knows or has prepared;
/// without any, searches the concentration spells of the catalog.
class ConcentrationSpellPicker extends ConsumerStatefulWidget {
  const ConcentrationSpellPicker({super.key, required this.character, this.scrollController});

  final CharacterDetail character;
  final ScrollController? scrollController;

  @override
  ConsumerState<ConcentrationSpellPicker> createState() => _ConcentrationSpellPickerState();
}

class _ConcentrationSpellPickerState extends ConsumerState<ConcentrationSpellPicker> {
  Timer? _debounce;
  String _search = '';

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _onSearch(String text) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      if (mounted) setState(() => _search = text.trim());
    });
  }

  /// Concentration spells the character can cast now, by name; null while the
  /// catalog detail loads.
  List<SpellDetail>? _ownSpells() {
    final c = widget.character;
    if (c.spells.isEmpty) return const [];
    final info = ref.watch(spellInfoProvider(spellInfoKey(c.spells.map((s) => s.spellIndex))));
    if (info.hasError) return const [];
    final details = info.value;
    if (details == null) return null;
    final seen = <String>{};
    return [
      for (final s in c.spells)
        if (details[s.spellIndex] case final SpellDetail d
            when d.concentration && isCastable(c, s, s.level ?? d.level) && seen.add(d.index))
          d,
    ]..sort((a, b) => foldForSort(a.name).compareTo(foldForSort(b.name)));
  }

  Widget _tile(SpellSummary spell) => ListTile(
    key: Key('pick-concentration-spell-${spell.index}'),
    title: Text(spell.name),
    subtitle: Text(spellLevelLabel(spell.level)),
    onTap: () => Navigator.of(context).pop(spell),
  );

  Widget _searchBody() {
    final results = ref.watch(
      _concentrationSearchProvider((campaignId: widget.character.campaignId, search: _search)),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Padding(
          padding: EdgeInsets.symmetric(horizontal: 16),
          child: Text(
            'No conoces ni tienes preparado ningún conjuro de concentración. '
            'Busca el conjuro en el catálogo.',
            key: Key('concentration-no-own-spells'),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
          child: TextField(
            key: const Key('concentration-search'),
            decoration: const InputDecoration(
              labelText: 'Buscar conjuro',
              prefixIcon: Icon(Icons.search),
            ),
            onChanged: _onSearch,
          ),
        ),
        Expanded(
          child: results.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, _) => Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('No se pudo buscar en el catálogo.'),
                TextButton(
                  onPressed: () => ref.invalidate(_concentrationSearchProvider),
                  child: const Text('Reintentar'),
                ),
              ],
            ),
            data: (spells) => spells.isEmpty
                ? const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Ningún conjuro de concentración coincide.'),
                  )
                : ListView(
                    key: const Key('concentration-spell-list'),
                    controller: widget.scrollController,
                    children: [for (final s in spells) _tile(s)],
                  ),
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final own = _ownSpells();
    final Widget body;
    if (own == null) {
      body = const Center(child: CircularProgressIndicator());
    } else if (own.isEmpty) {
      body = _searchBody();
    } else {
      body = ListView(
        key: const Key('concentration-spell-list'),
        controller: widget.scrollController,
        children: [for (final s in own) _tile(s)],
      );
    }
    return SafeArea(
      top: false,
      child: Column(
        key: const Key('concentration-picker'),
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 8, 8),
            child: Row(
              children: [
                Expanded(child: Text('Concentrarse en…', style: theme.textTheme.titleLarge)),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Cancelar'),
                ),
              ],
            ),
          ),
          Expanded(child: body),
        ],
      ),
    );
  }
}
