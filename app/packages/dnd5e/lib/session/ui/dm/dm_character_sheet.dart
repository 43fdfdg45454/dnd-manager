import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/router/app_router.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';
import 'package:opentrpg_core/core/theme/typography.dart';
import 'package:opentrpg_core/core/ui/offline_widgets.dart';
import 'package:opentrpg_core/features/campaigns/data/campaigns_controller.dart';
import 'package:opentrpg_core/features/characters/ui/change_owner_dialog.dart';
import 'package:opentrpg_core/features/session/ui/session_feedback.dart';

import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' show Condition;
import '../../../characters/models.dart' show CharacterCondition, DamageOutcome, classesLabel;
import '../../../characters/ui/character_tabs.dart' show titleFromSpellIndex;
import '../../../characters/ui/combat/combat_support.dart' show promptNumber;
import '../../../characters/ui/combat/concentration_flow.dart' show resolveDamageOutcome;
import '../../../characters/ui/combat/vitals_section.dart' show showConditionPicker;
import '../../party_controller.dart';
import '../../party_models.dart';
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
/// temporary and maximum hit points, conditions, the player it belongs to and a
/// link to the full sheet. Hit point and condition changes go through
/// `party/adjust`.
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

  Future<void> _adjust(PartyAdjustment adjustment, String success, {String? name}) async {
    var damage = const <DamageOutcome>[];
    final done = await runTableAction(context, () async {
      damage = await ref.read(partyControllerProvider(widget.campaignId).notifier).adjust([
        adjustment,
      ]);
    }, success: success);
    if (!done || !mounted) return;
    setState(_amount.clear);
    // Damage to a concentrated character asks for the Constitution save.
    for (final outcome in damage) {
      if (!mounted) return;
      await resolveDamageOutcome(
        context,
        ref,
        outcome,
        characterId: outcome.characterId,
        characterName: name,
      );
    }
  }

  Future<void> _damage(PartyMember m) async {
    final value = _value;
    if (value == null || value == 0) return;
    await _adjust(
      PartyAdjustment(characterId: m.id, hitPointsDelta: -value),
      '${m.name} recibe $value de daño.',
      name: m.name,
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
    final picked = await showConditionPicker(
      context,
      // Exhaustion has levels: the full sheet handles it.
      taken: {'exhaustion', for (final c in m.conditions) c.index},
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

  Future<void> _changeOwner(PartyMember m) async {
    final members =
        ref.read(campaignDetailControllerProvider(widget.campaignId)).value?.members ?? const [];
    final picked = await pickCharacterOwner(
      context,
      characterName: m.name,
      members: members,
      currentOwnerUserId: m.ownerUserId,
    );
    if (picked == null || !mounted) return;
    final name = members.where((p) => p.userId == picked.userId).firstOrNull?.displayName;
    await runTableAction(
      context,
      () => ref
          .read(partyControllerProvider(widget.campaignId).notifier)
          .setOwner(m.id, picked.userId),
      success: name == null ? '${m.name} es ahora un PNJ.' : '${m.name} es ahora de $name.',
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
              Row(
                children: [
                  Expanded(
                    child: Text(
                      m.ownerUserId == null
                          ? 'PNJ'
                          : 'Jugador: ${m.ownerDisplayName ?? 'Desconocido'}',
                      key: const Key('dm-sheet-owner'),
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
                  OfflineAware(
                    builder: (context, canWrite) => TextButton.icon(
                      key: const Key('character-owner'),
                      onPressed: canWrite ? () => _changeOwner(m) : null,
                      icon: const Icon(Icons.swap_horiz, size: 18),
                      label: const Text('Cambiar jugador'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
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
