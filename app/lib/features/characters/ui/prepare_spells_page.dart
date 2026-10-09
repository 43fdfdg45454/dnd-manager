import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/theme/components.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../../core/ui/spell_category.dart';
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/ui/catalog_detail_links.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';

/// "Prepara tus conjuros" (`/characters/:id/prepare-spells`): per class that
/// prepares, choose up to the maximum of the class list (the spellbook for
/// wizards). The player cannot leave it while the preparation is pending.
class PrepareSpellsPage extends ConsumerStatefulWidget {
  const PrepareSpellsPage({super.key, required this.characterId});

  final String characterId;

  @override
  ConsumerState<PrepareSpellsPage> createState() => _PrepareSpellsPageState();
}

class _PrepareSpellsPageState extends ConsumerState<PrepareSpellsPage> {
  /// Selection by class index; filled once from the server's current one.
  Map<String, Set<String>>? _selected;
  bool _busy = false;
  String? _error;

  CharacterController get _controller =>
      ref.read(characterControllerProvider(widget.characterId).notifier);

  void _init(SpellPreparation prep) {
    _selected ??= {
      for (final c in prep.classes) c.classIndex: {...c.prepared},
    };
  }

  bool _valid(SpellPreparation prep) {
    final selected = _selected;
    if (selected == null) return false;
    for (final c in prep.classes) {
      final n = selected[c.classIndex]?.length ?? 0;
      final min = c.candidates.isEmpty ? 0 : 1;
      if (n < min || n > c.max) return false;
    }
    return true;
  }

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
      if (!mounted) return;
      ref.invalidate(spellPreparationProvider(widget.characterId));
      _close();
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = problemDetail(error) ?? describeCharacterError(error);
      });
    }
  }

  void _close() {
    // The state was just updated (no longer pending), so the page may pop.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      if (context.canPop()) {
        context.pop();
      }
    });
  }

  Future<void> _confirm(SpellPreparation prep) => _run(
    () => _controller.prepareSpells({
      for (final c in prep.classes) c.classIndex: [...?_selected?[c.classIndex]],
    }),
  );

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(characterControllerProvider(widget.characterId));
    final prepAsync = ref.watch(spellPreparationProvider(widget.characterId));
    final pending = detail.value?.spellPreparationPending ?? false;
    final reason = detail.value?.spellPreparationReason;

    return PopScope(
      canPop: !pending || (!_busy && prepAsync.hasError),
      child: Scaffold(
        key: const Key('prepare-spells'),
        appBar: AppBar(
          title: const Text('Prepara tus conjuros'),
          automaticallyImplyLeading: !pending,
        ),
        body: prepAsync.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(describeCharacterError(error), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: () => ref.invalidate(spellPreparationProvider(widget.characterId)),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ),
          data: (prep) {
            _init(prep);
            return _buildBody(context, prep, reason ?? prep.reason);
          },
        ),
      ),
    );
  }

  Widget _buildBody(BuildContext context, SpellPreparation prep, SpellPreparationReason? reason) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final valid = _valid(prep);
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
            children: [
              if (reason != null)
                Text(
                  reason.label,
                  key: const Key('prepare-reason'),
                  style: theme.textTheme.titleMedium,
                ),
              if (prep.classes.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: 12),
                  child: Text('Este personaje no prepara conjuros.'),
                ),
              for (final c in prep.classes)
                _ClassSection(
                  key: ValueKey('prepare-class-${c.classIndex}'),
                  prepClass: c,
                  selected: _selected![c.classIndex]!,
                  onToggle: (index, on) => setState(() {
                    final set = _selected![c.classIndex]!;
                    on ? set.add(index) : set.remove(index);
                  }),
                ),
            ],
          ),
        ),
        SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      _error!,
                      key: const Key('prepare-error'),
                      style: TextStyle(color: scheme.error),
                    ),
                  ),
                if (!prep.canKeep && prep.keepProblem != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      prep.keepProblem!,
                      key: const Key('prepare-keep-problem'),
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
                OfflineAware(
                  builder: (context, canWrite) => Row(
                    children: [
                      if (prep.canKeep) ...[
                        Expanded(
                          child: OutlinedButton(
                            key: const Key('prepare-keep'),
                            onPressed: canWrite && !_busy
                                ? () => _run(_controller.keepSpellPreparation)
                                : null,
                            child: const Text('Mantener los de ayer'),
                          ),
                        ),
                        const SizedBox(width: 12),
                      ],
                      Expanded(
                        child: FilledButton(
                          key: const Key('prepare-confirm'),
                          onPressed: canWrite && !_busy && valid ? () => _confirm(prep) : null,
                          child: _busy
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Text('Preparar'),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// One class: counter, always-prepared spells, search, filters and the list.
class _ClassSection extends StatefulWidget {
  const _ClassSection({
    super.key,
    required this.prepClass,
    required this.selected,
    required this.onToggle,
  });

  final PreparationClass prepClass;
  final Set<String> selected;
  final void Function(String index, bool on) onToggle;

  @override
  State<_ClassSection> createState() => _ClassSectionState();
}

class _ClassSectionState extends State<_ClassSection> {
  String _query = '';
  int? _level;
  String? _category;

  @override
  Widget build(BuildContext context) {
    final c = widget.prepClass;
    final theme = Theme.of(context);
    final count = widget.selected.length;
    final full = count >= c.max;
    final levels = ({for (final s in c.candidates) s.level}.toList()..sort());
    final categories = [
      for (final cat in SpellCategory.values)
        if (c.candidates.any((s) => s.category == cat.apiValue)) cat,
    ];
    final query = _query.trim().toLowerCase();
    final shown = [
      for (final s in c.candidates)
        if ((_level == null || s.level == _level) &&
            (_category == null || s.category == _category) &&
            (query.isEmpty || s.name.toLowerCase().contains(query)))
          s,
    ];
    final key = c.classIndex;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(c.className, padding: const EdgeInsets.only(top: 16, bottom: 8)),
        Text(
          '$count de ${c.max}',
          key: Key('prepare-counter-$key'),
          style: theme.textTheme.titleMedium?.copyWith(
            color: count > c.max ? theme.colorScheme.error : null,
          ),
        ),
        if (c.alwaysPrepared.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('Siempre preparados', style: theme.textTheme.labelLarge),
          const SizedBox(height: 4),
          Wrap(
            spacing: 6,
            runSpacing: 4,
            children: [
              for (final s in c.alwaysPrepared)
                Chip(
                  key: Key('prepare-always-${s.index}'),
                  avatar: SpellCategoryIcon(s.category, size: 16),
                  label: Text(s.name),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
        ],
        const SizedBox(height: 8),
        TextField(
          key: Key('prepare-search-$key'),
          onChanged: (v) => setState(() => _query = v),
          decoration: const InputDecoration(
            labelText: 'Buscar conjuro',
            prefixIcon: Icon(Icons.search),
            isDense: true,
          ),
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 6,
          runSpacing: 4,
          children: [
            for (final l in levels)
              FilterChip(
                key: Key('prepare-level-$key-$l'),
                label: Text(spellLevelLabel(l)),
                selected: _level == l,
                onSelected: (on) => setState(() => _level = on ? l : null),
                visualDensity: VisualDensity.compact,
              ),
          ],
        ),
        if (categories.isNotEmpty)
          Wrap(
            spacing: 6,
            runSpacing: 4,
            children: [
              for (final cat in categories)
                FilterChip(
                  key: Key('prepare-category-$key-${cat.apiValue}'),
                  avatar: AppIconForCategory(cat),
                  label: Text(cat.label),
                  selected: _category == cat.apiValue,
                  onSelected: (on) => setState(() => _category = on ? cat.apiValue : null),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
        const SizedBox(height: 4),
        if (shown.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Text('Ningún conjuro coincide.'),
          ),
        for (final s in shown)
          CheckboxListTile(
            key: Key('prepare-spell-${s.index}'),
            dense: true,
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.trailing,
            value: widget.selected.contains(s.index),
            onChanged: !widget.selected.contains(s.index) && full
                ? null
                : (on) => widget.onToggle(s.index, on ?? false),
            secondary: SpellCategoryIcon(s.category),
            title: Row(
              children: [
                Expanded(child: Text(s.name)),
                DetailInfoButton(
                  key: Key('detail-spell-${s.index}'),
                  onPressed: () => openSpellDetail(context, s.index),
                ),
              ],
            ),
            subtitle: Text(
              [
                spellLevelLabel(s.level),
                ?s.school,
                if (s.concentration) 'Concentración',
                if (s.ritual) 'Ritual',
              ].join(' · '),
            ),
          ),
      ],
    );
  }
}

/// The category icon as the avatar of a filter chip (no tooltip noise).
class AppIconForCategory extends StatelessWidget {
  const AppIconForCategory(this.category, {super.key});

  final SpellCategory category;

  @override
  Widget build(BuildContext context) => SpellCategoryIcon(category.apiValue, size: 16);
}
