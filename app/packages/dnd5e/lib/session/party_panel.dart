import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/components.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/ui/offline_widgets.dart';
import 'package:opentrpg_core/features/campaigns/ui/confirm_dialog.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';
import 'package:opentrpg_core/features/session/data/session_controllers.dart';
import 'package:opentrpg_core/features/session/ui/dm/message_composer.dart';
import 'package:opentrpg_core/features/session/ui/session_feedback.dart';

import '../catalog/data/catalog_controllers.dart';
import 'party_controller.dart';
import 'party_models.dart';
import 'ui/dm/dm_character_sheet.dart';
import 'ui/dm/party_roster.dart';

/// The party block of the "Mesa del DM" (D&D 5e): the action bar (forced
/// rests, level grants, secret messages and dice) over the roster, whose
/// selection the actions apply to.
class Dnd5ePartyPanel extends ConsumerStatefulWidget {
  const Dnd5ePartyPanel({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<Dnd5ePartyPanel> createState() => _Dnd5ePartyPanelState();
}

class _Dnd5ePartyPanelState extends ConsumerState<Dnd5ePartyPanel> {
  String get _campaignId => widget.campaignId;

  /// Characters selected in the roster: rests and messages apply to them.
  final Set<String> _selected = {};

  void _toggle(String id) =>
      setState(() => _selected.contains(id) ? _selected.remove(id) : _selected.add(id));

  List<TableCharacter> _characters(List<PartyMember> party) => [
    for (final m in party) (id: m.id, name: m.name),
  ];

  /// Who an action applies to: the whole party or the selection.
  String _who(List<String> ids, List<PartyMember> party) => ids.isEmpty
      ? 'todo el grupo'
      : ids.length == 1
      ? party.firstWhere((m) => m.id == ids.single).name
      : '${ids.length} personajes';

  Future<void> _rest(PartyRestKind kind, List<PartyMember> party) async {
    final ids = _selected.where((id) => party.any((m) => m.id == id)).toList();
    final who = _who(ids, party);
    final confirmed = await confirmAction(
      context,
      title: kind.label,
      message: '¿Declarar un ${kind.label.toLowerCase()} para $who?',
      confirmLabel: 'Descansar',
    );
    if (!confirmed || !mounted) return;
    final done = await runTableAction(
      context,
      () => ref
          .read(partyControllerProvider(_campaignId).notifier)
          .rest(kind, characterIds: ids.isEmpty ? null : ids),
      success: '${kind.label} aplicado a $who.',
    );
    if (done && mounted) setState(_selected.clear);
  }

  Future<void> _grantLevel(List<PartyMember> party) async {
    final ids = _selected.where((id) => party.any((m) => m.id == id)).toList();
    final who = _who(ids, party);
    final confirmed = await confirmAction(
      context,
      title: 'Conceder nivel',
      message:
          '¿Conceder el siguiente nivel a $who? Cada jugador lo elegirá desde su sesión; '
          'quien ya tenga un nivel pendiente no acumula otro.',
      confirmLabel: 'Conceder',
    );
    if (!confirmed || !mounted) return;
    final done = await runTableAction(
      context,
      () => ref
          .read(partyControllerProvider(_campaignId).notifier)
          .grantLevel(characterIds: ids.isEmpty ? null : ids),
      success: 'Nivel concedido a $who.',
    );
    if (done && mounted) setState(_selected.clear);
  }

  Future<void> _message(List<PartyMember> party) async {
    final message = await showMessageComposer(
      context,
      characters: _characters(party),
      initial: _selected,
    );
    if (message == null || !mounted) return;
    await runTableAction(
      context,
      () => ref
          .read(messagesControllerProvider(_campaignId).notifier)
          .send(characterIds: message.characterIds, body: message.body),
      success: message.characterIds.length == 1 ? 'Mensaje enviado.' : 'Mensajes enviados.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final partyAsync = ref.watch(partyControllerProvider(_campaignId));
    final party = partyAsync.value ?? const <PartyMember>[];
    final conditions = ref.watch(conditionsProvider).value ?? const [];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _ActionBar(
          selectedCount: _selected.length,
          onShortRest: () => _rest(PartyRestKind.short, party),
          onLongRest: () => _rest(PartyRestKind.long, party),
          onGrantLevel: () => _grantLevel(party),
          onMessage: () => _message(party),
          onClearSelection: () => setState(_selected.clear),
        ),
        const SectionHeader('Grupo'),
        partyAsync.when(
          skipLoadingOnReload: true,
          loading: () => const Padding(
            padding: EdgeInsets.all(24),
            child: Center(child: CircularProgressIndicator()),
          ),
          error: (error, _) => Column(
            children: [
              Text(describeTableError(error), textAlign: TextAlign.center),
              TextButton(
                onPressed: () => ref.invalidate(partyControllerProvider(_campaignId)),
                child: const Text('Reintentar'),
              ),
            ],
          ),
          data: (members) => PartyRoster(
            members: members,
            selected: _selected,
            conditions: conditions,
            onToggle: _toggle,
            onOpen: (m) =>
                showDmCharacterSheet(context, campaignId: _campaignId, characterId: m.id),
          ),
        ),
      ],
    );
  }
}

class _ActionBar extends StatelessWidget {
  const _ActionBar({
    required this.selectedCount,
    required this.onShortRest,
    required this.onLongRest,
    required this.onGrantLevel,
    required this.onMessage,
    required this.onClearSelection,
  });

  final int selectedCount;
  final VoidCallback onShortRest;
  final VoidCallback onLongRest;
  final VoidCallback onGrantLevel;
  final VoidCallback onMessage;
  final VoidCallback onClearSelection;

  @override
  Widget build(BuildContext context) {
    final suffix = selectedCount == 0 ? '' : ' ($selectedCount)';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _ActionGrid(
          key: const Key('dm-action-grid'),
          children: [
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-short-rest'),
                onPressed: canWrite ? onShortRest : null,
                icon: AppIcons.campfire,
                label: 'Descanso corto$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-long-rest'),
                onPressed: canWrite ? onLongRest : null,
                icon: AppIcons.moon,
                label: 'Descanso largo$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('party-grant-level'),
                onPressed: canWrite ? onGrantLevel : null,
                icon: AppIcons.levelUp,
                label: 'Conceder nivel$suffix',
              ),
            ),
            OfflineAware(
              builder: (context, canWrite) => _ActionButton(
                key: const Key('dm-message'),
                onPressed: canWrite ? onMessage : null,
                icon: AppIcons.envelope,
                label: 'Mensaje secreto',
              ),
            ),
            _ActionButton(
              key: const Key('dm-dice'),
              onPressed: () => showDiceSheet(context),
              icon: AppIcons.d20,
              label: 'Dados',
            ),
          ],
        ),
        if (selectedCount > 0)
          TextButton.icon(
            key: const Key('dm-clear-selection'),
            onPressed: onClearSelection,
            icon: const Icon(Icons.deselect),
            label: Text(
              selectedCount == 1
                  ? '1 seleccionado · Quitar selección'
                  : '$selectedCount seleccionados · Quitar selección',
            ),
          ),
      ],
    );
  }
}

/// Equal boxes for the table's actions: two columns on a phone, one row of
/// [children].length from 600 px wide. Every box has the same width and
/// height whatever its label, so the row reads as one toolbar.
class _ActionGrid extends StatelessWidget {
  const _ActionGrid({super.key, required this.children});

  final List<Widget> children;

  static const _spacing = 8.0;
  static const _height = 44.0;
  static const _wideBreakpoint = 600.0;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth >= _wideBreakpoint ? children.length : 2;
        final width = (constraints.maxWidth - _spacing * (columns - 1)) / columns;
        return Wrap(
          spacing: _spacing,
          runSpacing: _spacing,
          children: [
            for (final child in children) SizedBox(width: width, height: _height, child: child),
          ],
        );
      },
    );
  }
}

/// A tonal button of the [_ActionGrid]; the label shrinks instead of
/// overflowing when the box is narrow.
class _ActionButton extends StatelessWidget {
  const _ActionButton({
    super.key,
    required this.onPressed,
    required this.icon,
    required this.label,
  });

  final VoidCallback? onPressed;
  final AppIcons icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return FilledButton.tonalIcon(
      onPressed: onPressed,
      style: FilledButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 12)),
      icon: AppIcon(icon, size: 20),
      label: FittedBox(fit: BoxFit.scaleDown, child: Text(label, maxLines: 1)),
    );
  }
}
