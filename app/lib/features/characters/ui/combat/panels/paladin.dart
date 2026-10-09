import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/motion/flash.dart';
import '../../../../../core/theme/app_icon.dart';
import '../../../../../core/theme/components.dart';
import '../../../../../core/theme/icons.dart';
import '../../../../dice/ui/dice_sheet.dart';
import '../../../data/characters_controller.dart';
import '../../../data/models.dart';
import '../combat_support.dart';
import 'critical_damage_roll.dart';
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

/// Most d8 Divine Smite can deal from the slot (2d8 + 1d8 per level above 1st).
const smiteMaxSlotDice = 5;

/// Most d8 Divine Smite can deal in total, with the die against undead and fiends.
const smiteMaxDice = 6;

/// Damage of a Divine Smite (PHB): the [base] dice the server gives for the
/// slot (at most 5d8), plus 1d8 [againstUndead] or fiends (at most 6d8), all
/// doubled on a [critical] hit. A [base] that is not "Nd8" is kept as it is.
String smiteDamage(String base, {bool againstUndead = false, bool critical = false}) {
  final match = RegExp(r'^\s*(\d+)\s*d\s*8\s*$', caseSensitive: false).firstMatch(base);
  if (match == null) {
    final extra = againstUndead ? '$base+1d8' : base;
    return criticalDamage(extra, critical: critical);
  }
  var count = int.parse(match.group(1)!);
  if (count > smiteMaxSlotDice) count = smiteMaxSlotDice;
  if (againstUndead) count = count + 1 > smiteMaxDice ? smiteMaxDice : count + 1;
  if (critical) count *= 2;
  return '${count}d8';
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

  /// Bumped on every Divine Smite the server accepts: plays the golden flash.
  int _smites = 0;

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
    setState(() => _smites++);
    final base = dice ?? '';
    final roll = await showDialog<({String damage, bool critical})>(
      context: context,
      builder: (_) => _SmiteDialog(base: base),
    );
    if (roll != null && mounted) {
      await rollAndShow(
        context,
        roll.damage,
        label: roll.critical ? 'Castigo divino (crítico)' : 'Castigo divino',
      );
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
        const SectionHeader('Paladín', padding: combatSectionPadding),
        CombatCard(
          key: const Key('class-panel-paladin'),
          title: 'Imposición de manos',
          trailing: Text(
            '$remaining / $pool',
            key: const Key('loh-remaining'),
            style: numericStyle(theme.textTheme.bodyMedium),
          ),
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
                    SizedBox(
                      width: 32,
                      child: Text(
                        '$amount',
                        key: const Key('loh-amount'),
                        style: numericStyle(theme.textTheme.bodyMedium),
                      ),
                    ),
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
                      icon: const AppIcon(AppIcons.drop, size: 20),
                      label: const Text('Curarme'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: FilledButton.tonalIcon(
                      key: const Key('loh-heal-other'),
                      onPressed: _canEdit && remaining > 0 ? () => _healOther(remaining) : null,
                      icon: const AppIcon(AppIcons.heart, size: 20),
                      label: const Text('Curar a otro'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        RadialFlash(
          key: const Key('smite-flash'),
          trigger: _smites == 0 ? null : _smites,
          child: CombatCard(
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
                      Text(
                        '+1d8 contra no-muertos e infernales.',
                        style: theme.textTheme.bodySmall,
                      ),
                      const SizedBox(height: 8),
                      FilledButton.icon(
                        key: const Key('smite-confirm'),
                        onPressed: _canEdit ? () => _smite(selected.level) : null,
                        icon: const AppIcon(AppIcons.sun, size: 20),
                        label: const Text('Castigo divino'),
                      ),
                    ],
                  ),
          ),
        ),
        CombatCard(
          title: 'Canalizar divinidad',
          trailing: Text(
            'Usos: $channelLeft / ${channel.max}',
            key: const Key('channel-uses'),
            style: numericStyle(theme.textTheme.bodyMedium),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              FilledButton.tonalIcon(
                key: const Key('channel-divinity'),
                onPressed: _canEdit && channelLeft > 0 ? _channel : null,
                icon: const AppIcon(AppIcons.sparkles, size: 20),
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

/// The extra radiant damage of a Divine Smite with the "Crítico" and
/// "Contra no muerto o infernal" boxes. Pops with what to roll.
class _SmiteDialog extends StatefulWidget {
  const _SmiteDialog({required this.base});

  /// Dice the server gives for the spent slot ("3d8").
  final String base;

  @override
  State<_SmiteDialog> createState() => _SmiteDialogState();
}

class _SmiteDialogState extends State<_SmiteDialog> {
  bool _critical = false;
  bool _undead = false;

  @override
  Widget build(BuildContext context) {
    final damage = widget.base.isEmpty
        ? ''
        : smiteDamage(widget.base, againstUndead: _undead, critical: _critical);
    return AlertDialog(
      title: const Text('Castigo divino'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Daño radiante adicional: $damage', key: const Key('smite-result')),
          if (damage.isNotEmpty) ...[
            const SizedBox(height: 8),
            CheckboxListTile(
              key: const Key('smite-critical'),
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: _critical,
              title: const Text('Crítico'),
              subtitle: const Text('Se duplican los dados.'),
              onChanged: (value) => setState(() => _critical = value ?? false),
            ),
            CheckboxListTile(
              key: const Key('smite-undead'),
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: _undead,
              title: const Text('Contra no muerto o infernal'),
              subtitle: const Text('+1d8, hasta un máximo de 6d8.'),
              onChanged: (value) => setState(() => _undead = value ?? false),
            ),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cerrar')),
        if (damage.isNotEmpty)
          FilledButton(
            key: const Key('smite-roll'),
            onPressed: () => Navigator.of(context).pop((damage: damage, critical: _critical)),
            child: Text('Tirar $damage'),
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
                    leading: const AppIcon(AppIcons.hood),
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
