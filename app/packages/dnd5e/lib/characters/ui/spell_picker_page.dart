import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/core/ui/infinite_scroll_list.dart';

import '../../catalog/data/catalog_repository.dart';
import '../../catalog/data/models.dart' show SpellSummary;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/ui/catalog_detail_links.dart';
import '../../ui/action_type.dart';
import '../../ui/spell_category.dart';
import '../models.dart';

/// A spellcasting class of the character being edited.
typedef SpellPickerClass = ({String classIndex, String className, int level});

/// Full-screen catalog search to add spells to a character. Spells are filtered
/// by class and by the highest spell level the class can cast. Pops the list of
/// spells added (empty if none).
///
/// The character creation wizard narrows it with [minLevel] / [maxLevel] (for
/// example only cantrips) and [limit] (how many more spells may be picked).
class SpellPickerPage extends ConsumerStatefulWidget {
  const SpellPickerPage({
    super.key,
    required this.classes,
    required this.chosen,
    this.minLevel = 0,
    this.maxLevel,
    this.limit,
    this.title = 'Añadir hechizos',
    this.campaignId,
  });

  /// The campaign of the character: only the spells of its content packs are
  /// offered (null: the global catalog).
  final String? campaignId;

  final List<SpellPickerClass> classes;

  /// Lowest and highest spell level offered (0 = cantrips); [maxLevel] null
  /// means whatever the class can cast.
  final int minLevel;
  final int? maxLevel;

  /// Most spells that may be picked in this page; null means no limit.
  final int? limit;
  final String title;

  /// Spell indexes the character already has (shown checked, not toggleable).
  final Set<String> chosen;

  @override
  ConsumerState<SpellPickerPage> createState() => _SpellPickerPageState();
}

class _SpellPickerPageState extends ConsumerState<SpellPickerPage> {
  static const _pageSize = 50;

  final _searchController = TextEditingController();
  final _picked = <CharacterSpell>[];
  Timer? _debounce;
  late SpellPickerClass _class = widget.classes.first;
  late int? _levelFilter = widget.maxLevel != null && widget.maxLevel == widget.minLevel
      ? widget.minLevel
      : null;
  int _maxLevel = 9;
  List<SpellSummary> _items = [];
  int _page = 1;
  bool _hasMore = false;
  bool _loading = true;
  Object? _error;
  int _generation = 0;

  CatalogRepository get _catalog => ref.read(catalogRepositoryProvider);

  @override
  void initState() {
    super.initState();
    _reset();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  /// Highest spell level with slots for the class at its current level (9 if unknown).
  Future<int> _maxSpellLevel(SpellPickerClass cls) async {
    try {
      final detail = await _catalog.classDetail(cls.classIndex);
      final level = detail.levels.where((l) => l.level == cls.level).firstOrNull;
      if (level == null) return 9;
      final highest = level.spellSlots.lastIndexWhere((s) => s > 0);
      return highest + 1; // 0 when no slots: cantrips only
    } catch (_) {
      return 9;
    }
  }

  Future<void> _reset() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
      _items = [];
      _page = 1;
    });
    try {
      final maxLevel = await _maxSpellLevel(_class);
      if (generation != _generation || !mounted) return;
      _maxLevel = widget.maxLevel == null ? maxLevel : maxLevel.clamp(0, widget.maxLevel!);
      if (_levelFilter != null && _levelFilter! > maxLevel) _levelFilter = null;
      await _fetch(generation, 1);
    } catch (error) {
      if (generation != _generation || !mounted) return;
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  Future<void> _fetch(int generation, int page) async {
    final result = await _catalog.spells(
      search: _searchController.text,
      level: _levelFilter,
      classIndex: _class.classIndex,
      page: page,
      pageSize: _pageSize,
      campaignId: widget.campaignId,
    );
    if (generation != _generation || !mounted) return;
    setState(() {
      _items = [
        ..._items,
        ...result.items.where((s) => s.level <= _maxLevel && s.level >= widget.minLevel),
      ];
      _page = page;
      _hasMore = result.hasMore;
      _loading = false;
    });
  }

  /// Next page for the infinite scroll. Pages whose spells are all filtered out
  /// locally (level range) are skipped so the list always grows. Errors reach
  /// the list, which reports them and offers a retry.
  Future<void> _loadMore() async {
    final generation = _generation;
    final before = _items.length;
    while (mounted && generation == _generation && _hasMore && _items.length == before) {
      await _fetch(generation, _page + 1);
    }
  }

  void _onSearchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), _reset);
  }

  bool _isPicked(String index) => _picked.any((s) => s.spellIndex == index);

  void _toggle(SpellSummary spell) {
    setState(() {
      if (_isPicked(spell.index)) {
        _picked.removeWhere((s) => s.spellIndex == spell.index);
      } else if (widget.limit != null && _picked.length >= widget.limit!) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text('Como máximo ${widget.limit} más.')));
      } else {
        _picked.add(
          CharacterSpell(
            spellIndex: spell.index,
            classIndex: _class.classIndex,
            name: spell.name,
            level: spell.level,
            category: spell.category,
            isPrepared: spell.level == 0,
          ),
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.title),
        actions: [
          TextButton(
            key: const Key('spell-picker-done'),
            onPressed: () => Navigator.of(context).pop<List<CharacterSpell>>(_picked),
            child: Text(
              _picked.isEmpty
                  ? 'Listo'
                  : widget.limit == null
                  ? 'Añadir (${_picked.length})'
                  : 'Añadir (${_picked.length}/${widget.limit})',
            ),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: Column(
              children: [
                if (widget.classes.length > 1)
                  DropdownButtonFormField<String>(
                    key: const Key('spell-picker-class'),
                    initialValue: _class.classIndex,
                    decoration: const InputDecoration(labelText: 'Clase'),
                    items: [
                      for (final c in widget.classes)
                        DropdownMenuItem(value: c.classIndex, child: Text(c.className)),
                    ],
                    onChanged: (value) {
                      _class = widget.classes.firstWhere((c) => c.classIndex == value);
                      _reset();
                    },
                  ),
                const SizedBox(height: 8),
                TextField(
                  key: const Key('spell-picker-search'),
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  decoration: const InputDecoration(
                    labelText: 'Buscar hechizo',
                    prefixIcon: Icon(Icons.search),
                  ),
                ),
                const SizedBox(height: 8),
                DropdownButtonFormField<int?>(
                  key: const Key('spell-picker-level'),
                  initialValue: _levelFilter,
                  decoration: const InputDecoration(labelText: 'Nivel'),
                  items: [
                    const DropdownMenuItem<int?>(value: null, child: Text('Todos los niveles')),
                    for (var l = widget.minLevel; l <= _maxLevel; l++)
                      DropdownMenuItem<int?>(value: l, child: Text(spellLevelLabel(l))),
                  ],
                  onChanged: (value) {
                    _levelFilter = value;
                    _reset();
                  },
                ),
              ],
            ),
          ),
          Expanded(child: _buildList(context)),
        ],
      ),
    );
  }

  Widget _buildList(BuildContext context) {
    if (_error != null && _items.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(describeApiError(_error!), textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: _reset,
                icon: const Icon(Icons.refresh),
                label: const Text('Reintentar'),
              ),
            ],
          ),
        ),
      );
    }
    if (_loading && _items.isEmpty) return const Center(child: CircularProgressIndicator());
    if (_items.isEmpty) {
      return const Center(child: Text('Ningún hechizo coincide con la búsqueda.'));
    }
    return InfiniteScrollList(
      listKey: const Key('spell-picker-list'),
      itemCount: _items.length,
      hasMore: _hasMore,
      onLoadMore: _loadMore,
      itemBuilder: (context, i) {
        final spell = _items[i];
        // Spells chosen before opening the picker stay checked and disabled.
        final locked = widget.chosen.contains(spell.index);
        final toggle = locked ? null : () => _toggle(spell);
        return ListTile(
          key: Key('picker-spell-${spell.index}'),
          enabled: !locked,
          onTap: toggle,
          leading: SpellCategoryIcon(spell.category),
          title: Text(spell.name),
          subtitle: CastingTimeSubtitle(
            castingTime: spell.castingTime,
            text: [
              spellLevelLabel(spell.level),
              if (spell.school != null) spell.school!,
              if (spell.concentration) 'Concentración',
            ].join(' · '),
          ),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              DetailInfoButton(
                key: Key('detail-spell-${spell.index}'),
                onPressed: () => openSpellDetail(context, spell.index),
              ),
              Checkbox(
                value: locked || _isPicked(spell.index),
                onChanged: toggle == null ? null : (_) => toggle(),
              ),
            ],
          ),
        );
      },
    );
  }
}
