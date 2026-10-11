import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/motion/vignette.dart';
import 'package:opentrpg_core/core/storage/local_preferences.dart';
import 'package:opentrpg_core/core/systems/game_system_ui.dart' show GameSystemUi;
import 'package:opentrpg_core/core/ui/short_screen.dart';

import '../../../session/level_up_card.dart';
import '../../../session/ui/player/combat_items_section.dart';
import '../../models.dart';
import 'attacks_section.dart';
import 'class_panels.dart';
import 'companion_section.dart';
import 'resources_section.dart';
import 'rest_section.dart';
import 'spells_section.dart';
import 'status_section.dart';
import 'vitals_section.dart';

/// The tabs of the combat view, in order.
enum CombatSection {
  actions('Acciones'),
  spells('Conjuros'),
  resources('Recursos'),
  status('Estado'),
  more('Más');

  const CombatSection(this.label);

  final String label;
}

/// Preference with the last tab of the combat view of [characterId].
String combatSectionPreference(String characterId) => 'character.$characterId.combatSection';

/// The tabs [character] has something in: "Conjuros" with spells, slots or
/// class rows for it, "Recursos" with limited uses; "Acciones", "Estado" and
/// "Más" always.
List<CombatSection> combatSectionsOf(CharacterDetail character) => [
  CombatSection.actions,
  if (character.spells.isNotEmpty ||
      regularSlots(character).isNotEmpty ||
      pactSlotsOf(character) != null ||
      hasClassSpellRows(character))
    CombatSection.spells,
  if (hasResources(character)) CombatSection.resources,
  CombatSection.status,
  CombatSection.more,
];

/// Height of the row of section chips.
const _sectionBarHeight = 48.0;

/// The combat screen of a character: a compact status header (two rows: hit
/// points with the death saves at 0, then the quick damage and healing with
/// the one-line summary of the states) over one row of five tabs that scrolls
/// sideways, the last one chosen remembered per character:
///
/// - "Acciones": attacks, the spells it can cast in one tap and what each
///   class does on its turn.
/// - "Conjuros": slots, the spells by level ("Preparados" / "Todos"), rituals
///   and "Preparar conjuros".
/// - "Recursos": every limited use with its pips, recharge and origin.
/// - "Estado": the conditions (a tap on the summary of the header opens
///   them), then armor class, initiative, speed, passive perception,
///   proficiency bonus and inspiration, each with its breakdown.
/// - "Más": companions, consumables, what the classes need to remember,
///   recoveries and rests.
///
/// In portrait the header and the tabs never scroll. On a short screen (a
/// phone in landscape, [isShortScreen]) only the tabs stay pinned: the header
/// (with [scrollingHeader] over it, the app bar of the page) scrolls away
/// with the content and snaps back as soon as the user scrolls up a little.
///
/// A tab with nothing to show loses its chip. Every action calls the server
/// and then refreshes the character; nothing changes locally when a request
/// fails.
///
/// [canEdit] false (a player looking at someone else's character) disables
/// every control that writes. [isDm] (a DM or the Owner) rests the character
/// directly; anyone else asks the DM for the rest. [inSession] is the
/// player's own view of "Mi sesión": the level
/// granted on top of the tabs, the combat items instead of the quick
/// consumables and no rests (they live in the "Sesión" sub-tab). At 0 hit
/// points a dark vignette closes in on the edges.
class CombatView extends ConsumerStatefulWidget {
  const CombatView({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
    this.inSession = false,
    this.scrollingHeader,
  });

  final CharacterDetail character;
  final bool canEdit;
  final bool isDm;
  final bool inSession;

  /// Goes over the status header and scrolls with it (see
  /// [GameSystemUi.combatView]).
  final Widget? scrollingHeader;

  @override
  ConsumerState<CombatView> createState() => _CombatViewState();
}

class _CombatViewState extends ConsumerState<CombatView> with SingleTickerProviderStateMixin {
  CombatSection? _section;

  /// How the floating header snaps in or out when a drag ends.
  final _snap = FloatingHeaderSnapConfiguration();

  /// Height of the status header when it floats (a short screen), measured
  /// from the header itself; an estimate until the first layout.
  double _headerHeight = 96;

  String get _preference => combatSectionPreference(widget.character.id);

  CombatSection? _stored() {
    try {
      final name = ref.read(localPreferencesProvider)?.getString(_preference);
      return CombatSection.values.where((s) => s.name == name).firstOrNull;
    } catch (_) {
      return null;
    }
  }

  void _select(CombatSection section) {
    setState(() => _section = section);
    try {
      ref.read(localPreferencesProvider)?.setString(_preference, section.name);
    } catch (_) {
      // Remembering the tab is a convenience: without storage it is not kept.
    }
  }

  void _headerMeasured(Size size) {
    if (size.height == _headerHeight) return;
    // Reported during layout: the extent changes on the next frame.
    SchedulerBinding.instance.addPostFrameCallback((_) {
      if (mounted) setState(() => _headerHeight = size.height);
    });
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.character;
    final sections = combatSectionsOf(c);
    final wanted = _section ??= _stored();
    final section = sections.contains(wanted) ? wanted! : sections.first;
    final status = CombatStatusHeader(
      character: c,
      canEdit: widget.canEdit,
      onShowStates: () => _select(CombatSection.status),
    );
    final header = switch (widget.scrollingHeader) {
      null => status,
      // An app bar has no height of its own: it gets its preferred one (plus
      // the status bar it covers).
      final PreferredSizeWidget top => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            height: top.preferredSize.height + MediaQuery.paddingOf(context).top,
            child: top,
          ),
          status,
        ],
      ),
      final top => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [top, status],
      ),
    };
    final bar = _SectionBar(sections: sections, selected: section, onSelected: _select);
    final padding = EdgeInsets.fromLTRB(16, 0, 16, 96 + MediaQuery.paddingOf(context).bottom);
    final children = [
      if (widget.inSession && c.pendingLevelUpTo != null) LevelUpCard(character: c),
      ..._children(section),
    ];
    // Keyed by section: a new tab starts at the top (the header in view).
    final sectionKey = Key('combat-section-${section.name}');
    final Widget body;
    if (isShortScreen(context)) {
      body = CustomScrollView(
        key: sectionKey,
        slivers: [
          SliverPersistentHeader(
            floating: true,
            delegate: _FixedExtentHeader(
              extent: _headerHeight,
              child: _MeasuredBox(onSize: _headerMeasured, child: header),
              vsync: this,
              snapConfiguration: _snap,
            ),
          ),
          SliverPersistentHeader(
            pinned: true,
            delegate: _FixedExtentHeader(extent: _sectionBarHeight, child: bar),
          ),
          SliverPadding(
            padding: padding,
            sliver: SliverList.list(children: children),
          ),
        ],
      );
    } else {
      body = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          header,
          bar,
          Expanded(
            child: ListView(key: sectionKey, padding: padding, children: children),
          ),
        ],
      );
    }
    final keyed = KeyedSubtree(
      key: Key(widget.inSession ? 'player-combat' : 'combat-view'),
      child: body,
    );
    // "Mi sesión" darkens its own edges.
    return widget.inSession ? keyed : DarkVignette(active: c.hitPointsCurrent == 0, child: keyed);
  }

  List<Widget> _children(CombatSection section) {
    final c = widget.character;
    final canEdit = widget.canEdit;
    final isDm = widget.isDm;
    return switch (section) {
      CombatSection.actions => [
        AttacksSection(character: c),
        SpellsSection(character: c, canEdit: canEdit),
        ClassPanelGroups(character: c, part: ClassPanelPart.actions, canEdit: canEdit, isDm: isDm),
      ],
      CombatSection.spells => [
        SpellListSection(
          character: c,
          canEdit: canEdit,
          isDm: isDm,
          spellRows: [
            ClassPanelGroups(
              character: c,
              part: ClassPanelPart.spells,
              canEdit: canEdit,
              isDm: isDm,
            ),
          ],
        ),
      ],
      CombatSection.resources => [ResourcesSection(character: c, canEdit: canEdit, isDm: isDm)],
      CombatSection.status => [CombatStatusSection(character: c, canEdit: canEdit)],
      CombatSection.more => [
        CompanionSection(character: c, canEdit: canEdit, isDm: isDm),
        if (widget.inSession)
          CombatItemsSection(character: c, canEdit: canEdit)
        else
          ConsumablesSection(character: c, canEdit: canEdit),
        ClassPanelGroups(character: c, part: ClassPanelPart.more, canEdit: canEdit, isDm: isDm),
        if (!widget.inSession) RestSection(character: c, canEdit: canEdit, isDm: isDm),
      ],
    };
  }
}

/// One chip per tab of the combat view, keyed `combat-tab-<section>`, in a
/// single row that scrolls sideways when the chips do not fit. Opaque, since
/// on a short screen the content scrolls under it.
class _SectionBar extends StatelessWidget {
  const _SectionBar({required this.sections, required this.selected, required this.onSelected});

  final List<CombatSection> sections;
  final CombatSection selected;
  final ValueChanged<CombatSection> onSelected;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Theme.of(context).scaffoldBackgroundColor,
      child: SizedBox(
        height: _sectionBarHeight,
        child: SingleChildScrollView(
          key: const Key('combat-tabs'),
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Row(
            children: [
              for (final section in sections)
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    key: Key('combat-tab-${section.name}'),
                    label: Text(section.label),
                    selected: section == selected,
                    showCheckmark: false,
                    onSelected: (_) => onSelected(section),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A persistent header of a fixed [extent] that shows [child].
class _FixedExtentHeader extends SliverPersistentHeaderDelegate {
  const _FixedExtentHeader({
    required this.extent,
    required this.child,
    this.vsync,
    this.snapConfiguration,
  });

  final double extent;
  final Widget child;

  /// Both set, a floating header snaps fully in or out when the drag ends.
  @override
  final TickerProvider? vsync;

  @override
  final FloatingHeaderSnapConfiguration? snapConfiguration;

  @override
  double get minExtent => extent;

  @override
  double get maxExtent => extent;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) => child;

  @override
  bool shouldRebuild(_FixedExtentHeader oldDelegate) =>
      oldDelegate.extent != extent ||
      oldDelegate.child != child ||
      oldDelegate.vsync != vsync ||
      oldDelegate.snapConfiguration != snapConfiguration;
}

/// Lays [child] out at its own height, whatever the height given, and reports
/// that height ([onSize]) whenever it changes; the floating header takes its
/// extent from it.
class _MeasuredBox extends StatelessWidget {
  const _MeasuredBox({required this.onSize, required this.child});

  final ValueChanged<Size> onSize;
  final Widget child;

  @override
  Widget build(BuildContext context) => OverflowBox(
    alignment: Alignment.topCenter,
    minHeight: 0,
    maxHeight: double.infinity,
    child: _SizeReporter(onSize: onSize, child: child),
  );
}

class _SizeReporter extends SingleChildRenderObjectWidget {
  const _SizeReporter({required this.onSize, required super.child});

  final ValueChanged<Size> onSize;

  @override
  RenderObject createRenderObject(BuildContext context) => _RenderSizeReporter(onSize);

  @override
  void updateRenderObject(BuildContext context, _RenderSizeReporter renderObject) {
    renderObject.onSize = onSize;
  }
}

class _RenderSizeReporter extends RenderProxyBox {
  _RenderSizeReporter(this.onSize);

  ValueChanged<Size> onSize;
  Size? _reported;

  @override
  void performLayout() {
    super.performLayout();
    if (size != _reported) {
      _reported = size;
      onSize(size);
    }
  }
}
