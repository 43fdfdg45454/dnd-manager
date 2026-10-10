import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../items/ui/inventory_tab.dart';
import '../data/models.dart';
import '../data/view_mode_controller.dart';
import '../domain/character_format.dart';
import 'character_tabs.dart';

/// Sub-tabs of the sheet, in the order of [CharacterTab.detailTabs].
const _sheetTabs = [
  Tab(key: Key('tab-summary'), text: 'Resumen'),
  Tab(key: Key('tab-skills'), text: 'Habilidades'),
  Tab(key: Key('tab-traits'), text: 'Rasgos'),
  Tab(key: Key('tab-spells'), text: 'Hechizos'),
  Tab(key: Key('tab-inventory'), text: 'Inventario'),
  Tab(key: Key('tab-notes'), text: 'Notas'),
];

/// "Detalle" of a character: a secondary bar of sub-tabs (Resumen,
/// Habilidades, Rasgos, Hechizos, Inventario, Notas) over their content.
/// Shared by the character page and the player's session, which adds a first
/// "Sesión" sub-tab with [session]. The last sheet sub-tab is remembered per
/// character ([characterTabProvider]); with [session], whether it was chosen
/// is remembered too ([playerSessionTabProvider]).
class CharacterDetailTabs extends ConsumerStatefulWidget {
  const CharacterDetailTabs({
    super.key,
    required this.character,
    required this.permissions,
    this.session,
  });

  final CharacterDetail character;
  final CharacterPermissions permissions;

  /// Content of the extra first sub-tab "Sesión" (only in "Mi sesión").
  final Widget? session;

  @override
  ConsumerState<CharacterDetailTabs> createState() => _CharacterDetailTabsState();
}

class _CharacterDetailTabsState extends ConsumerState<CharacterDetailTabs>
    with SingleTickerProviderStateMixin {
  late final TabController _controller;

  bool get _hasSession => widget.session != null;

  int get _offset => _hasSession ? 1 : 0;

  String get _id => widget.character.id;

  @override
  void initState() {
    super.initState();
    final onSession = _hasSession && ref.read(playerSessionTabProvider(_id));
    final tab = ref.read(characterTabProvider(_id));
    _controller = TabController(
      length: _sheetTabs.length + _offset,
      initialIndex: onSession ? 0 : _offset + CharacterTab.detailTabs.indexOf(tab),
      vsync: this,
    )..addListener(_remember);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// Stores the sub-tab once the change settles (not on every animation tick).
  void _remember() {
    if (_controller.indexIsChanging) return;
    final index = _controller.index;
    if (_hasSession) ref.read(playerSessionTabProvider(_id).notifier).select(index == 0);
    if (index < _offset) return;
    ref.read(characterTabProvider(_id).notifier).select(CharacterTab.detailTabs[index - _offset]);
  }

  @override
  Widget build(BuildContext context) {
    final character = widget.character;
    final session = widget.session;
    // Its own (transparent) Material: the tiles of the sub-tabs paint their
    // ink here and not under a coloured background of the host page.
    return Material(
      type: MaterialType.transparency,
      child: Column(
        children: [
          TabBar.secondary(
            key: const Key('character-detail-tabs'),
            controller: _controller,
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              if (session != null) const Tab(key: Key('tab-session'), text: 'Sesión'),
              ..._sheetTabs,
            ],
          ),
          Expanded(
            child: TabBarView(
              controller: _controller,
              children: [
                ?session,
                SummaryTab(
                  character: character,
                  canEdit: widget.permissions.canEdit,
                  isDm: widget.permissions.isDm,
                ),
                SkillsTab(character: character),
                TraitsTab(character: character),
                SpellsTab(character: character),
                InventoryTab(character: character),
                NotesTab(character: character),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
