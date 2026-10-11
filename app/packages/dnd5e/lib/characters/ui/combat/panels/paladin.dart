import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/motion/flash.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/features/characters/data/characters_controller.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';

import '../../../../ui/action_type.dart';
import '../../../models.dart';
import '../combat_rows.dart';
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

/// Damage of a Divine Smite: the [base] dice the server gives for the
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

/// Paladin: Lay on Hands, Divine Sense, Divine Smite and Channel Divinity on
/// the turn; the Aura of Protection to remember.
const paladinPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) => [
  LayOnHandsRow(panel: panel),
  const FeatureReminder(
    key: Key('paladin-divine-sense'),
    icon: AppIcons.eye,
    title: 'Sentido divino',
    featureIndex: 'divine-sense',
    // SRD: "As an action, you can open your awareness to detect such forces".
    actionKind: ActionKind.action,
    text:
        'Hasta el final de tu próximo turno conoces la ubicación de celestiales, '
        'infernales y muertos vivientes a 60 pies que no estén tras cobertura total. '
        'Usos: 1 + tu modificador de Carisma por descanso largo.',
  ),
  DivineSmiteRow(panel: panel),
  _ChannelDivinityRow(panel: panel),
];

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final aura = panelInt(panel.panel.data['auraRange']);
  return [
    if (aura != null)
      FeatureReminder(
        key: const Key('paladin-aura'),
        icon: AppIcons.sun,
        title: 'Aura de protección',
        featureIndex: 'aura-of-protection',
        text: 'Aura de protección: $aura pies',
      ),
  ];
}

/// Lay on Hands: the pool left, a slider for the amount and "Curarme" /
/// "Curar a otro".
class LayOnHandsRow extends ConsumerStatefulWidget {
  const LayOnHandsRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  ConsumerState<LayOnHandsRow> createState() => _LayOnHandsRowState();
}

class _LayOnHandsRowState extends ConsumerState<LayOnHandsRow> {
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

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final loh = panelMap(widget.panel.panel.data['layOnHands']);
    final pool = panelInt(loh['pool']) ?? 0;
    final used = panelInt(loh['used']) ?? 0;
    final remaining = (pool - used).clamp(0, pool);
    final amount = remaining == 0 ? 0 : _amount.clamp(1, remaining);
    return CombatActionRow(
      icon: AppIcons.drop,
      title: 'Imposición de manos',
      // SRD: "As an action, you can touch a creature and draw power from the pool".
      actionKind: ActionKind.action,
      cost: '$remaining / $pool',
      costKey: const Key('loh-remaining'),
      subtitle: 'Cura PG de tu reserva, o 5 por enfermedad o veneno.',
      info: const FeatureInfoButton(index: 'lay-on-hands'),
      details: [
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
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Text(remaining == 0 ? 'Reserva agotada.' : 'Cantidad: 1'),
          ),
      ],
      actions: [
        FilledButton.icon(
          key: const Key('loh-heal-self'),
          onPressed: _canEdit && remaining > 0 ? () => _healSelf(remaining) : null,
          icon: const AppIcon(AppIcons.drop, size: 18),
          label: const Text('Curarme'),
        ),
        FilledButton.tonalIcon(
          key: const Key('loh-heal-other'),
          onPressed: _canEdit && remaining > 0 ? () => _healOther(remaining) : null,
          icon: const AppIcon(AppIcons.heart, size: 18),
          label: const Text('Curar a otro'),
        ),
      ],
    );
  }
}

/// Divine Smite: picks the slot level and spends it, then offers to roll the
/// extra damage. A golden flash plays on every smite the server accepts.
class DivineSmiteRow extends ConsumerStatefulWidget {
  const DivineSmiteRow({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  ConsumerState<DivineSmiteRow> createState() => _DivineSmiteRowState();
}

class _DivineSmiteRowState extends ConsumerState<DivineSmiteRow> {
  int? _smiteLevel;

  /// Bumped on every Divine Smite the server accepts: plays the golden flash.
  int _smites = 0;

  Future<void> _smite(int level) async {
    String? dice;
    final done = await runCombat(context, () async {
      dice = await panelController(ref, widget.panel.character).divineSmite(level);
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

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final smite = panelMap(widget.panel.panel.data['divineSmite']);
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
    return RadialFlash(
      key: const Key('smite-flash'),
      trigger: _smites == 0 ? null : _smites,
      child: CombatActionRow(
        icon: AppIcons.sun,
        title: 'Castigo divino',
        cost: selected == null ? null : 'Espacio de nivel ${selected.level}',
        subtitle: slots.isEmpty
            ? 'No tienes espacios de conjuro disponibles.'
            : 'Al impactar cuerpo a cuerpo, gasta un espacio: +1d8 contra no-muertos e '
                  'infernales.',
        info: const FeatureInfoButton(index: 'divine-smite'),
        action: selected == null
            ? null
            : FilledButton.icon(
                key: const Key('smite-confirm'),
                onPressed: widget.panel.canEdit ? () => _smite(selected.level) : null,
                icon: const AppIcon(AppIcons.sun, size: 18),
                label: const Text('Castigar'),
              ),
        details: [
          if (selected != null) ...[
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final slot in slots)
                  ChoiceChip(
                    key: Key('smite-level-${slot.level}'),
                    label: Text('Nivel ${slot.level} (${slot.available})'),
                    selected: slot.level == selected.level,
                    onSelected: (_) => setState(() => _smiteLevel = slot.level),
                  ),
              ],
            ),
            Text(
              'Dados extra: ${selected.dice}',
              key: const Key('smite-dice'),
              style: theme.textTheme.titleSmall,
            ),
          ],
        ],
      ),
    );
  }
}

/// Channel Divinity of the paladin (uses from the server's panel).
class _ChannelDivinityRow extends ConsumerWidget {
  const _ChannelDivinityRow({required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = panel.character;
    final channel = panelUses(panel.panel.data['channelDivinity']);
    final left = channel.max - channel.used;

    Future<void> use() async {
      final resource = findResource(c, 'channel-divinity', 'channel divinity');
      if (resource == null) {
        showCombatMessage(context, 'No se encontró el recurso Canalizar divinidad.');
        return;
      }
      await runCombat(
        context,
        () => panelController(ref, c).spendResource(resource.id),
        success: 'Canalizar divinidad gastado.',
      );
    }

    return CombatActionRow(
      icon: AppIcons.sparkles,
      title: 'Canalizar divinidad',
      // SRD, Oath of Devotion: Sacred Weapon and Turn the Unholy are both used
      // "As an action".
      actionKind: ActionKind.action,
      cost: 'Usos: $left / ${channel.max}',
      costKey: const Key('channel-uses'),
      subtitle: 'Las opciones de tu juramento.',
      info: const FeatureInfoButton(index: 'channel-divinity'),
      action: FilledButton.tonalIcon(
        key: const Key('channel-divinity'),
        onPressed: panel.canEdit && left > 0 ? use : null,
        icon: const AppIcon(AppIcons.sparkles, size: 18),
        label: const Text('Canalizar'),
      ),
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
