import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/ui/detail_widgets.dart' show SectionTitle;
import '../../../dice/ui/dice_sheet.dart';
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../character_tabs.dart' show titleFromSpellIndex;
import 'combat_state.dart';
import 'combat_support.dart';
import 'resources_section.dart' show regularSlots, resourcesOf;

/// What a class panel needs to render.
class ClassPanelContext {
  const ClassPanelContext({required this.character, required this.panel, required this.canEdit});

  final CharacterDetail character;
  final ClassPanel panel;
  final bool canEdit;
}

typedef ClassPanelBuilder = Widget Function(BuildContext context, ClassPanelContext panel);

/// Class panels by `classIndex`. A class without an entry gets
/// [buildGenericPanel]. Register new classes here.
final Map<String, ClassPanelBuilder> classPanelBuilders = {
  'barbarian': (context, panel) => BarbarianPanel(panel: panel),
  'wizard': (context, panel) => WizardPanel(panel: panel),
  'paladin': (context, panel) => PaladinPanel(panel: panel),
};

/// The panels of the character's classes, each with the registered builder.
class ClassPanelsSection extends StatelessWidget {
  const ClassPanelsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  @override
  Widget build(BuildContext context) {
    final panels = [
      for (final panel in character.combat.classPanels)
        (classPanelBuilders[panel.classIndex] ?? buildGenericPanel)(
          context,
          ClassPanelContext(character: character, panel: panel, canEdit: canEdit),
        ),
    ];
    if (panels.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: panels);
  }
}

// -- Data helpers -------------------------------------------------------------

int? _int(Object? value) =>
    value is num ? value.toInt() : (value is String ? int.tryParse(value) : null);

Map<String, dynamic> _map(Object? value) =>
    value is Map ? Map<String, dynamic>.from(value) : const {};

({int max, int used}) _uses(Object? value) {
  final map = _map(value);
  return (max: _int(map['max']) ?? 0, used: _int(map['used']) ?? 0);
}

List<String> _strings(Object? value) => value is List ? [for (final e in value) '$e'] : const [];

String _className(CharacterDetail character, ClassPanel panel, String fallback) {
  for (final c in character.classes) {
    if (c.classIndex == panel.classIndex) return c.className;
  }
  return fallback;
}

CharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

/// Finds a resource by key or by a name fragment (case-insensitive).
CharacterResource? findResource(CharacterDetail character, String key, String nameFragment) {
  for (final r in resourcesOf(character)) {
    if (r.key == key || r.name.toLowerCase().contains(nameFragment.toLowerCase())) return r;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Generic
// ---------------------------------------------------------------------------

/// Panel for classes without their own: the class and any extra data the
/// server sends. Class resources are listed in the Resources section.
Widget buildGenericPanel(BuildContext context, ClassPanelContext panel) {
  final data = panel.panel.data;
  if (data.isEmpty) return const SizedBox.shrink();
  return Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      SectionTitle(
        '${_className(panel.character, panel.panel, titleFromSpellIndex(panel.panel.classIndex))} '
        '(nivel ${panel.panel.level})',
      ),
      CombatCard(
        key: Key('class-panel-${panel.panel.classIndex}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (final e in data.entries)
              if (e.value is! Map && e.value is! List) Text('${e.key}: ${e.value}'),
          ],
        ),
      ),
    ],
  );
}

// ---------------------------------------------------------------------------
// Barbarian
// ---------------------------------------------------------------------------

class BarbarianPanel extends ConsumerWidget {
  const BarbarianPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final c = panel.character;
    final data = panel.panel.data;
    final uses = _uses(data['rageUses']);
    final bonus = _int(data['rageDamageBonus']) ?? 0;
    final brutal = _int(data['brutalCriticalDice']) ?? 0;
    final ac = _int(data['unarmoredDefenseAc']);
    final rage = ref.watch(rageControllerProvider(c.id));
    final rageController = ref.read(rageControllerProvider(c.id).notifier);
    final remaining = uses.max - uses.used;

    Future<void> startRage() async {
      final done = await runCombat(
        context,
        () => _controller(ref, c).rage(),
        success: 'Furia activada.',
      );
      if (done) rageController.start();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Bárbaro'),
        CombatCard(
          key: const Key('class-panel-barbarian'),
          title: 'Furia',
          trailing: Text('Usos: $remaining / ${uses.max}', key: const Key('rage-uses')),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (rage.active) ...[
                Container(
                  key: const Key('rage-active'),
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.errorContainer,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.local_fire_department),
                          const SizedBox(width: 8),
                          Text('Furia activa', style: theme.textTheme.titleMedium),
                        ],
                      ),
                      Text('Bono de daño cuerpo a cuerpo: +$bonus'),
                      Text('Asaltos restantes: ${rage.roundsLeft}', key: const Key('rage-rounds')),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        key: const Key('rage-next-round'),
                        onPressed: rageController.nextRound,
                        child: const Text('Siguiente asalto'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: OutlinedButton(
                        key: const Key('rage-end'),
                        onPressed: rageController.end,
                        child: const Text('Terminar furia'),
                      ),
                    ),
                  ],
                ),
              ] else
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    key: const Key('rage-start'),
                    onPressed: panel.canEdit && remaining > 0 ? startRage : null,
                    icon: const Icon(Icons.local_fire_department),
                    label: Text(remaining > 0 ? 'Furia' : 'Furia (sin usos)'),
                  ),
                ),
              const Divider(height: 24),
              if (data['recklessAttack'] == true)
                ListTile(
                  key: const Key('reckless-attack'),
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.bolt),
                  title: const Text('Ataque temerario'),
                  subtitle: const Text(
                    'Ventaja en tus ataques cuerpo a cuerpo con Fuerza este turno, pero los '
                    'ataques contra ti tienen ventaja hasta tu próximo turno.',
                  ),
                ),
              if (ac != null) Text('Defensa sin armadura: CA $ac'),
              if (brutal > 0)
                Text(
                  'Crítico brutal: $brutal ${brutal == 1 ? 'dado' : 'dados'} de daño adicional '
                  'en un crítico cuerpo a cuerpo.',
                ),
            ],
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Wizard
// ---------------------------------------------------------------------------

class WizardPanel extends ConsumerWidget {
  const WizardPanel({super.key, required this.panel});

  final ClassPanelContext panel;

  Future<void> _arcaneRecovery(BuildContext context, WidgetRef ref, int budget) async {
    final levels = await showDialog<List<int>>(
      context: context,
      builder: (_) => ArcaneRecoveryDialog(character: panel.character, budget: budget),
    );
    if (levels == null || levels.isEmpty || !context.mounted) return;
    await runCombat(
      context,
      () => _controller(ref, panel.character).arcaneRecovery(levels),
      success: 'Recuperación arcana aplicada.',
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final data = panel.panel.data;
    final spellbook = _strings(data['spellbook']);
    final prepared = _strings(data['prepared']).toSet();
    final preparedMax = _int(data['preparedMax']);
    final recovery = _map(data['arcaneRecovery']);
    final recovered = recovery['used'] == true || (_int(recovery['used']) ?? 0) > 0;
    final budget = _int(recovery['slotLevelsRecoverable']) ?? (panel.panel.level + 1) ~/ 2;
    final info = spellbook.isEmpty
        ? null
        : ref.watch(spellInfoProvider(spellInfoKey(spellbook))).value;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Mago'),
        CombatCard(
          key: const Key('class-panel-wizard'),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ExpansionTile(
                key: const Key('wizard-spellbook'),
                tilePadding: EdgeInsets.zero,
                title: const Text('Libro de hechizos'),
                subtitle: Text(
                  preparedMax == null
                      ? 'Preparados: ${prepared.length}'
                      : 'Preparados: ${prepared.length} / $preparedMax',
                  key: const Key('wizard-prepared-count'),
                ),
                children: [
                  if (spellbook.isEmpty) const ListTile(title: Text('El libro está vacío.')),
                  for (final index in spellbook)
                    ListTile(
                      key: Key('spellbook-$index'),
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      leading: Icon(
                        prepared.contains(index) ? Icons.check_circle : Icons.circle_outlined,
                        size: 20,
                        semanticLabel: prepared.contains(index) ? 'Preparado' : 'No preparado',
                      ),
                      title: Text(info?[index]?.name ?? titleFromSpellIndex(index)),
                      subtitle: info?[index] == null
                          ? null
                          : Text(
                              info![index]!.level == 0 ? 'Truco' : 'Nivel ${info[index]!.level}',
                            ),
                    ),
                ],
              ),
              const Divider(height: 24),
              FilledButton.tonalIcon(
                key: const Key('arcane-recovery'),
                onPressed: panel.canEdit && !recovered
                    ? () => _arcaneRecovery(context, ref, budget)
                    : null,
                icon: const Icon(Icons.auto_fix_high),
                label: Text(recovered ? 'Recuperación arcana (usada)' : 'Recuperación arcana'),
              ),
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Tras un descanso corto, recupera espacios de conjuro cuyo nivel sumado sea '
                  '$budget como máximo (ninguno de nivel 6 o superior). Una vez por día.',
                  style: theme.textTheme.bodySmall,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Picks the spell slots to recover: any number per level while the sum of the
/// levels stays within [budget] and no slot above level 5 is chosen. Resolves
/// to the chosen levels, one entry per slot (`[1, 1, 2]`).
class ArcaneRecoveryDialog extends StatefulWidget {
  const ArcaneRecoveryDialog({super.key, required this.character, required this.budget});

  final CharacterDetail character;
  final int budget;

  @override
  State<ArcaneRecoveryDialog> createState() => _ArcaneRecoveryDialogState();
}

class _ArcaneRecoveryDialogState extends State<ArcaneRecoveryDialog> {
  final Map<int, int> _chosen = {};

  int get _sum => _chosen.entries.fold(0, (s, e) => s + e.key * e.value);

  @override
  Widget build(BuildContext context) {
    final slots = [
      for (final s in regularSlots(widget.character))
        if (s.level <= 5) s,
    ];
    final left = widget.budget - _sum;
    return AlertDialog(
      title: const Text('Recuperación arcana'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Niveles seleccionados: $_sum / ${widget.budget}', key: const Key('arcane-sum')),
            const SizedBox(height: 8),
            if (slots.isEmpty) const Text('No tienes espacios de nivel 5 o inferior.'),
            for (final slot in slots)
              Row(
                key: Key('arcane-level-${slot.level}'),
                children: [
                  Expanded(child: Text('Nivel ${slot.level} (gastados ${slot.used})')),
                  IconButton(
                    key: Key('arcane-level-${slot.level}-minus'),
                    tooltip: 'Menos',
                    onPressed: (_chosen[slot.level] ?? 0) > 0
                        ? () => setState(() => _chosen[slot.level] = _chosen[slot.level]! - 1)
                        : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  SizedBox(
                    width: 24,
                    child: Text('${_chosen[slot.level] ?? 0}', textAlign: TextAlign.center),
                  ),
                  IconButton(
                    key: Key('arcane-level-${slot.level}-plus'),
                    tooltip: 'Más',
                    onPressed: (_chosen[slot.level] ?? 0) < slot.used && slot.level <= left
                        ? () => setState(() => _chosen[slot.level] = (_chosen[slot.level] ?? 0) + 1)
                        : null,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('arcane-confirm'),
          onPressed: _sum == 0
              ? null
              : () => Navigator.of(context).pop(
                  [
                    for (final e in _chosen.entries)
                      for (var i = 0; i < e.value; i++) e.key,
                  ]..sort(),
                ),
          child: const Text('Recuperar'),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Paladin
// ---------------------------------------------------------------------------

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
  bool _targetSelf = true;

  CharacterDetail get _character => widget.panel.character;
  bool get _canEdit => widget.panel.canEdit;

  Future<void> _layOnHands(int remaining) async {
    final amount = _amount.clamp(1, remaining);
    await runCombat(
      context,
      () => _controller(ref, _character).layOnHands(amount, targetSelf: _targetSelf),
      success: _targetSelf ? 'Te curas $amount PG.' : 'Gastas $amount puntos de la reserva.',
    );
  }

  Future<void> _smite(int level) async {
    String? dice;
    final done = await runCombat(context, () async {
      dice = await _controller(ref, _character).divineSmite(level);
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
      () => _controller(ref, _character).spendResource(resource.id),
      success: 'Canalizar divinidad gastado.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final data = widget.panel.panel.data;
    final loh = _map(data['layOnHands']);
    final pool = _int(loh['pool']) ?? 0;
    final used = _int(loh['used']) ?? 0;
    final remaining = (pool - used).clamp(0, pool);
    final smite = _map(data['divineSmite']);
    final slots = [
      for (final e in (smite['slotsByLevel'] as List? ?? const []))
        if (e is Map)
          _SmiteSlot(
            level: _int(e['level']) ?? 0,
            available: _int(e['available']) ?? 0,
            dice: _smiteDice(e['extraDice']),
          ),
    ].where((s) => s.available > 0).toList();
    final selected = slots.where((s) => s.level == _smiteLevel).firstOrNull ?? slots.firstOrNull;
    final channel = _uses(data['channelDivinity']);
    final channelLeft = channel.max - channel.used;
    final aura = _int(data['auraRange']);
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
              SwitchListTile(
                key: const Key('loh-self'),
                contentPadding: EdgeInsets.zero,
                title: const Text('Sobre mí'),
                subtitle: const Text('Desactívalo para curar a otra criatura.'),
                value: _targetSelf,
                onChanged: _canEdit ? (v) => setState(() => _targetSelf = v) : null,
              ),
              FilledButton.icon(
                key: const Key('loh-apply'),
                onPressed: _canEdit && remaining > 0 ? () => _layOnHands(remaining) : null,
                icon: const Icon(Icons.back_hand_outlined),
                label: const Text('Imponer manos'),
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
