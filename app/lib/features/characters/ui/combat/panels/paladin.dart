import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../catalog/ui/detail_widgets.dart' show SectionTitle;
import '../../../../dice/ui/dice_sheet.dart';
import '../../../data/characters_controller.dart';
import '../../../data/models.dart';
import '../combat_support.dart';
import 'panel_support.dart';

/// One entry of `divineSmite.slotsByLevel`.
class _SmiteSlot {
  const _SmiteSlot({required this.level, required this.available, required this.dice});

  final int level;
  final int available;

  /// Extra damage dice: "2d8".
  final String dice;
}

/// `extraDice` may be a count (2) or text ("2d8").
String _smiteDice(Object? raw) {
  if (raw is num) return '${raw.toInt()}d8';
  final text = '$raw'.trim();
  return RegExp(r'^\d+$').hasMatch(text) ? '${text}d8' : text;
}

class PaladinPanel extends ConsumerStatefulWidget {
  const PaladinPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  ConsumerState<PaladinPanel> createState() => _PaladinPanelState();
}

class _PaladinPanelState extends ConsumerState<PaladinPanel> {
  int? _smiteLevel;
  int _amount = 1;

  CharacterDetail get _character => widget.panel.character;
  bool get _canEdit => widget.panel.canEdit;

  Future<void> _healSelf(int remaining) async {
    final amount = _amount.clamp(1, remaining);
    await runCombat(
      context,
      () => panelController(ref, _character).layOnHands(amount),
      success: 'Te curas $amount PG.',
    );
  }

  Future<void> _healOther(int remaining) async {
    final target = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => _TargetSheet(campaignId: _character.campaignId, ownId: _character.id),
    );
    if (target == null || !mounted) return;
    final amount = _amount.clamp(1, remaining);
    await runCombat(
      context,
      () => panelController(
        ref,
        _character,
      ).layOnHands(amount, targetSelf: false, note: 'Curar a $target'),
      success: 'Has curado $amount PG a $target.',
    );
  }

  Future<void> _smite(int level) async {
    String? dice;
    final done = await runCombat(context, () async {
      dice = await panelController(ref, _character).divineSmite(level);
    });
    if (!done || !mounted) return;
    final damage = dice ?? '';
    final roll = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Castigo divino'),
        content: Text('Daño radiante adicional: $damage', key: const Key('smite-result')),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cerrar'),
          ),
          if (damage.isNotEmpty)
            FilledButton(
              key: const Key('smite-roll'),
              onPressed: () => Navigator.of(dialogContext).pop(true),
              child: Text('Tirar $damage'),
            ),
        ],
      ),
    );
    if (roll == true && mounted) {
      await rollAndShow(context, damage, label: 'Castigo divino');
    }
  }

  Future<void> _channel() async {
    final resource = findResource(_character, 'channel-divinity', 'channel divinity');
    if (resource == null) {
      showCombatMessage(context, 'No se encontró el recurso Canalizar divinidad.');
      return;
    }
    await runCombat(
      context,
      () => panelController(ref, _character).spendResource(resource.id),
      success: 'Canalizar divinidad gastado.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final data = widget.panel.panel.data;
    final loh = panelMap(data['layOnHands']);
    final pool = panelInt(loh['pool']) ?? 0;
    final used = panelInt(loh['used']) ?? 0;
    final remaining = (pool - used).clamp(0, pool);
    final smite = panelMap(data['divineSmite']);
    final slots = [
      for (final e in (smite['slotsByLevel'] as List? ?? const []))
        if (e is Map)
          _SmiteSlot(
            level: panelInt(e['level']) ?? 0,
            available: panelInt(e['available']) ?? 0,
            dice: _smiteDice(e['extraDice']),
          ),
    ].where((s) => s.available > 0).toList();
    final selected = slots.where((s) => s.level == _smiteLevel).firstOrNull ?? slots.firstOrNull;
    final channel = panelUses(data['channelDivinity']);
    final channelLeft = channel.max - channel.used;
    final aura = panelInt(data['auraRange']);
    final amount = remaining == 0 ? 0 : _amount.clamp(1, remaining);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Paladín'),
        CombatCard(
          key: const Key('class-panel-paladin'),
          title: 'Imposición de manos',
          trailing: Text('$remaining / $pool', key: const Key('loh-remaining')),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              LinearProgressIndicator(
                value: pool == 0 ? 0 : remaining / pool,
                minHeight: 10,
                borderRadius: BorderRadius.circular(5),
              ),
              if (remaining > 1)
                Row(
                  children: [
                    Expanded(
                      child: Slider(
                        key: const Key('loh-slider'),
                        min: 1,
                        max: remaining.toDouble(),
                        divisions: remaining - 1,
                        value: amount.toDouble(),
                        label: '$amount',
                        onChanged: _canEdit ? (v) => setState(() => _amount = v.round()) : null,
                      ),
                    ),
                    SizedBox(width: 32, child: Text('$amount', key: const Key('loh-amount'))),
                  ],
                )
              else
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Text(remaining == 0 ? 'Reserva agotada.' : 'Cantidad: 1'),
                ),
              Row(
                children: [
                  Expanded(
                    child: FilledButton.icon(
                      key: const Key('loh-heal-self'),
                      onPressed: _canEdit && remaining > 0 ? () => _healSelf(remaining) : null,
                      icon: const Icon(Icons.back_hand_outlined),
                      label: const Text('Curarme'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: FilledButton.tonalIcon(
                      key: const Key('loh-heal-other'),
                      onPressed: _canEdit && remaining > 0 ? () => _healOther(remaining) : null,
                      icon: const Icon(Icons.favorite_border),
                      label: const Text('Curar a otro'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        CombatCard(
          title: 'Castigo divino',
          child: slots.isEmpty
              ? const Text('No tienes espacios de conjuro disponibles.')
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: 8,
                      children: [
                        for (final slot in slots)
                          ChoiceChip(
                            key: Key('smite-level-${slot.level}'),
                            label: Text('Nivel ${slot.level} (${slot.available})'),
                            selected: slot.level == selected!.level,
                            onSelected: (_) => setState(() => _smiteLevel = slot.level),
                          ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Dados extra: ${selected!.dice}',
                      key: const Key('smite-dice'),
                      style: theme.textTheme.titleMedium,
                    ),
                    Text('+1d8 contra no-muertos e infernales.', style: theme.textTheme.bodySmall),
                    const SizedBox(height: 8),
                    FilledButton.icon(
                      key: const Key('smite-confirm'),
                      onPressed: _canEdit ? () => _smite(selected.level) : null,
                      icon: const Icon(Icons.flare),
                      label: const Text('Castigo divino'),
                    ),
                  ],
                ),
        ),
        CombatCard(
          title: 'Canalizar divinidad',
          trailing: Text('Usos: $channelLeft / ${channel.max}', key: const Key('channel-uses')),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              FilledButton.tonalIcon(
                key: const Key('channel-divinity'),
                onPressed: _canEdit && channelLeft > 0 ? _channel : null,
                icon: const Icon(Icons.brightness_7_outlined),
                label: const Text('Canalizar divinidad'),
              ),
              if (aura != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text('Aura de protección: $aura pies'),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Bottom sheet that picks who is healed: a character of the campaign or a free
/// name. Pops with the chosen name.
class _TargetSheet extends ConsumerStatefulWidget {
  const _TargetSheet({required this.campaignId, required this.ownId});

  final String campaignId;
  final String ownId;

  @override
  ConsumerState<_TargetSheet> createState() => _TargetSheetState();
}

class _TargetSheetState extends ConsumerState<_TargetSheet> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final characters = ref.watch(campaignCharactersControllerProvider(widget.campaignId));
    final others = [
      for (final c in characters.value ?? const <CharacterSummary>[])
        if (c.id != widget.ownId) c,
    ];
    final name = _controller.text.trim();
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: SingleChildScrollView(
          key: const Key('loh-other-sheet'),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('¿A quién curas?', style: theme.textTheme.titleMedium),
              const SizedBox(height: 8),
              if (characters.isLoading && others.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 12),
                  child: Center(child: CircularProgressIndicator()),
                )
              else if (characters.hasError && others.isEmpty)
                const Text('No se pudo cargar el grupo. Escribe el nombre abajo.')
              else
                for (final c in others)
                  ListTile(
                    key: Key('loh-target-${c.id}'),
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.person_outline),
                    title: Text(c.name),
                    subtitle: c.ownerDisplayName == null ? null : Text(c.ownerDisplayName!),
                    onTap: () => Navigator.of(context).pop(c.name),
                  ),
              const SizedBox(height: 8),
              TextField(
                key: const Key('loh-target-free'),
                controller: _controller,
                textCapitalization: TextCapitalization.sentences,
                onChanged: (_) => setState(() {}),
                onSubmitted: (_) => name.isEmpty ? null : Navigator.of(context).pop(name),
                decoration: const InputDecoration(
                  labelText: 'Otra criatura',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 8),
              FilledButton(
                key: const Key('loh-target-free-confirm'),
                onPressed: name.isEmpty ? null : () => Navigator.of(context).pop(name),
                child: const Text('Curar'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
