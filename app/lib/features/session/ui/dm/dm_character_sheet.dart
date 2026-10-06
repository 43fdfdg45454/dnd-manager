import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../characters/data/models.dart' show CharacterCondition, classesLabel;
import '../../../characters/ui/character_tabs.dart' show titleFromSpellIndex;
import '../../../characters/ui/combat/combat_support.dart' show promptNumber;
import '../../../characters/ui/combat/vitals_section.dart' show ConditionPickerDialog;
import '../../data/models.dart';
import '../../data/session_controllers.dart';
import '../session_feedback.dart';
import 'party_roster.dart' show HpBar;

/// Opens the quick controls of the DM for one character of the party.
Future<void> showDmCharacterSheet(
  BuildContext context, {
  required String campaignId,
  required String characterId,
}) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  useRootNavigator: true,
  showDragHandle: true,
  builder: (_) => DmCharacterSheet(campaignId: campaignId, characterId: characterId),
);

/// Bottom sheet of the DM for one party member: quick damage and healing,
/// temporary and maximum hit points, conditions and a link to the full sheet.
/// Every change goes through `party/adjust`.
class DmCharacterSheet extends ConsumerStatefulWidget {
  const DmCharacterSheet({super.key, required this.campaignId, required this.characterId});

  final String campaignId;
  final String characterId;

  @override
  ConsumerState<DmCharacterSheet> createState() => _DmCharacterSheetState();
}

class _DmCharacterSheetState extends ConsumerState<DmCharacterSheet> {
  final _amount = TextEditingController();

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  int? get _value {
    final value = int.tryParse(_amount.text.trim());
    return value == null || value < 0 ? null : value;
  }

  Future<void> _adjust(PartyAdjustment adjustment, String success) async {
    final done = await runTableAction(
      context,
      () => ref.read(partyControllerProvider(widget.campaignId).notifier).adjust([adjustment]),
      success: success,
    );
    if (done && mounted) setState(_amount.clear);
  }

  Future<void> _damage(PartyMember m) async {
    final value = _value;
    if (value == null || value == 0) return;
    await _adjust(
      PartyAdjustment(characterId: m.id, hitPointsDelta: -value),
      '${m.name} recibe $value de daño.',
    );
  }

  Future<void> _heal(PartyMember m) async {
    final value = _value;
    if (value == null || value == 0) return;
    await _adjust(
      PartyAdjustment(characterId: m.id, hitPointsDelta: value),
      '${m.name} recupera $value PG.',
    );
  }

  Future<void> _temporary(PartyMember m) async {
    final value = _value;
    if (value == null) return;
    await _adjust(
      PartyAdjustment(characterId: m.id, temporaryHitPoints: value),
      'PG temporales de ${m.name}: $value.',
    );
  }

  Future<void> _maximum(PartyMember m) async {
    final value = await promptNumber(
      context,
      title: 'PG máximos de ${m.name}',
      label: 'PG máximos (0 = volver al cálculo)',
      initial: m.hitPointsMax,
      max: 9999,
      confirmLabel: 'Guardar',
    );
    if (value == null || !mounted) return;
    await _adjust(
      PartyAdjustment(characterId: m.id, hitPointsMax: value),
      value == 0 ? 'PG máximos calculados de nuevo.' : 'PG máximos actualizados.',
    );
  }

  Future<void> _addCondition(PartyMember m) async {
    final picked = await showDialog<Condition>(
      context: context,
      builder: (_) => ConditionPickerDialog(
        // Exhaustion has levels: the full sheet handles it.
        taken: {'exhaustion', for (final c in m.conditions) c.index},
      ),
    );
    if (picked == null || !mounted) return;
    await _adjust(
      PartyAdjustment(
        characterId: m.id,
        addConditions: [CharacterCondition(index: picked.index)],
      ),
      'Condición añadida: ${picked.name}.',
    );
  }

  Future<void> _removeCondition(PartyMember m, String index, String name) => _adjust(
    PartyAdjustment(characterId: m.id, removeConditions: [index]),
    'Condición quitada: $name.',
  );

  @override
  Widget build(BuildContext context) {
    final party = ref.watch(partyControllerProvider(widget.campaignId)).value ?? const [];
    final m = party.where((p) => p.id == widget.characterId).firstOrNull;
    if (m == null) {
      return const SizedBox(
        height: 160,
        child: Center(child: Text('Este personaje ya no está en el grupo.')),
      );
    }
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final names = {
      for (final c in ref.watch(conditionsProvider).value ?? const <Condition>[]) c.index: c.name,
    };
    final hasValue = _value != null && _value! > 0;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: SingleChildScrollView(
          key: const Key('dm-character-sheet'),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(m.name, style: theme.textTheme.headlineSmall),
              Text(
                '${m.classes.isEmpty ? 'Sin clase' : classesLabel(m.classes)} · Nivel ${m.level}',
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: 12),
              HpBar(member: m),
              const SizedBox(height: 4),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'PG ${m.hitPointsCurrent} / ${m.hitPointsMax}'
                      '${m.temporaryHitPoints > 0 ? ' · ${m.temporaryHitPoints} temporales' : ''}',
                      key: const Key('dm-sheet-hp'),
                      style: theme.textTheme.titleMedium?.merge(AppTypography.numeric),
                    ),
                  ),
                  OfflineAware(
                    builder: (context, canWrite) => TextButton.icon(
                      key: const Key('dm-hp-max'),
                      onPressed: canWrite ? () => _maximum(m) : null,
                      icon: const AppIcon(AppIcons.quill, size: 18),
                      label: const Text('PG máx.'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              TextField(
                key: const Key('dm-amount'),
                controller: _amount,
                keyboardType: TextInputType.number,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  labelText: 'Cantidad',
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
              ),
              const SizedBox(height: 8),
              OfflineAware(
                builder: (context, canWrite) => Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    FilledButton.icon(
                      key: const Key('dm-damage'),
                      style: FilledButton.styleFrom(backgroundColor: tokens.crimson),
                      onPressed: canWrite && hasValue ? () => _damage(m) : null,
                      icon: const AppIcon(AppIcons.splash, size: 20),
                      label: const Text('Daño'),
                    ),
                    FilledButton.icon(
                      key: const Key('dm-heal'),
                      style: FilledButton.styleFrom(backgroundColor: tokens.emerald),
                      onPressed: canWrite && hasValue ? () => _heal(m) : null,
                      icon: const AppIcon(AppIcons.drop, size: 20),
                      label: const Text('Curar'),
                    ),
                    OutlinedButton.icon(
                      key: const Key('dm-temp'),
                      onPressed: canWrite && _value != null ? () => _temporary(m) : null,
                      icon: const AppIcon(AppIcons.shield, size: 20),
                      label: const Text('Fijar temporales'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Text('Condiciones', style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              OfflineAware(
                builder: (context, canWrite) => Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    for (final c in m.conditions)
                      InputChip(
                        key: Key('dm-condition-${c.index}'),
                        avatar: AppIcon(AppIcons.chains, size: 16, color: tokens.boneMuted),
                        label: Text(names[c.index] ?? titleFromSpellIndex(c.index)),
                        deleteButtonTooltipMessage: 'Quitar',
                        onDeleted: canWrite
                            ? () => _removeCondition(
                                m,
                                c.index,
                                names[c.index] ?? titleFromSpellIndex(c.index),
                              )
                            : null,
                      ),
                    if (m.conditions.isEmpty) const Text('Sin condiciones.'),
                    ActionChip(
                      key: const Key('dm-condition-add'),
                      avatar: const Icon(Icons.add, size: 18),
                      label: const Text('Añadir'),
                      onPressed: canWrite ? () => _addCondition(m) : null,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  key: const Key('dm-open-sheet'),
                  onPressed: () {
                    final router = GoRouter.of(context);
                    Navigator.of(context).pop();
                    router.push(AppRoutes.character(m.id));
                  },
                  icon: const AppIcon(AppIcons.scroll, size: 20),
                  label: const Text('Ver hoja completa'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
