import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/characters/models.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../items/ui/inventory_tab.dart';
import '../data/view_mode_controller.dart';
import '../domain/character_permissions.dart';
import 'notes_tab.dart';

/// "Detalle" of a character: a secondary bar of sub-tabs over their content.
/// The sub-tabs of the game system come first ([GameSystemUi.detailTabs];
/// D&D 5e: Resumen, Habilidades, Rasgos, Hechizos), then the ones of the core
/// (Inventario, Notas). Shared by the character page and the player's
/// session, which adds a first "Sesión" sub-tab with [session]. The last sheet
/// sub-tab is remembered per character ([characterTabProvider], by
/// [SheetTab.id]); with [session], whether it was chosen is remembered too
/// ([playerSessionTabProvider]).
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
    with TickerProviderStateMixin {
  TabController? _controller;

  /// The ids of the sheet sub-tabs [_controller] was made for.
  List<String> _tabIds = const [];

  bool get _hasSession => widget.session != null;

  int get _offset => _hasSession ? 1 : 0;

  String get _id => widget.character.id;

  List<SheetTab> _sheetTabs() {
    final character = widget.character;
    final system = ref.watch(campaignSystemUiProvider(character.campaignId));
    return [
      ...system.detailTabs(
        character,
        canEdit: widget.permissions.canEdit,
        isDm: widget.permissions.isDm,
      ),
      SheetTab(
        id: 'inventory',
        label: 'Inventario',
        builder: (_) => InventoryTab(character: character),
      ),
      SheetTab(
        id: 'notes',
        label: 'Notas',
        builder: (_) => NotesTab(character: character),
      ),
    ];
  }

  /// A controller for [tabs], on the remembered sub-tab (or "Sesión").
  TabController _controllerFor(List<SheetTab> tabs) {
    final onSession = _hasSession && ref.read(playerSessionTabProvider(_id));
    final stored = ref.read(characterTabProvider(_id));
    final tab = tabs.indexWhere((t) => t.id == stored);
    return TabController(
      length: tabs.length + _offset,
      initialIndex: onSession ? 0 : _offset + (tab < 0 ? 0 : tab),
      vsync: this,
    )..addListener(_remember);
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  /// Stores the sub-tab once the change settles (not on every animation tick).
  void _remember() {
    final controller = _controller;
    if (controller == null || controller.indexIsChanging) return;
    final index = controller.index;
    if (_hasSession) ref.read(playerSessionTabProvider(_id).notifier).select(index == 0);
    if (index < _offset) return;
    ref.read(characterTabProvider(_id).notifier).select(_tabIds[index - _offset]);
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _sheetTabs();
    final ids = [for (final tab in tabs) tab.id];
    if (_controller == null || !_sameIds(ids, _tabIds)) {
      // First build, or the game system of the campaign is now known and
      // brings other sub-tabs. The old controller goes once the bar let it go.
      final old = _controller?..removeListener(_remember);
      if (old != null) WidgetsBinding.instance.addPostFrameCallback((_) => old.dispose());
      _tabIds = ids;
      _controller = _controllerFor(tabs);
    }
    final controller = _controller!;
    final session = widget.session;
    // Its own (transparent) Material: the tiles of the sub-tabs paint their
    // ink here and not under a coloured background of the host page.
    return Material(
      type: MaterialType.transparency,
      child: Column(
        children: [
          TabBar.secondary(
            key: const Key('character-detail-tabs'),
            controller: controller,
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              if (session != null) const Tab(key: Key('tab-session'), text: 'Sesión'),
              for (final tab in tabs) Tab(key: tab.tabKey, text: tab.label),
            ],
          ),
          Expanded(
            child: TabBarView(
              controller: controller,
              children: [
                ?session,
                for (final tab in tabs) Builder(builder: tab.builder),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

bool _sameIds(List<String> a, List<String> b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}
