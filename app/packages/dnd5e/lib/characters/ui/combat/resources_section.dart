import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/ui/stat_value.dart';
import 'package:opentrpg_core/features/characters/data/characters_controller.dart';
import 'package:opentrpg_core/features/dice/ui/dice_sheet.dart';
import 'package:opentrpg_core/features/items/data/inventory_repository.dart';
import 'package:opentrpg_core/features/items/data/items_controllers.dart';
import 'package:opentrpg_core/features/items/data/models.dart' show CharacterItem;
import 'package:opentrpg_core/features/items/ui/effective_item_page.dart'
    show openInventoryItemDetail;

import '../../../catalog/ui/catalog_detail_links.dart';
import '../../dnd5e_characters_controller.dart';
import '../../models.dart';
import 'combat_rows.dart';
import 'combat_support.dart';

Dnd5eCharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(dnd5eCharacterControllerProvider(character.id).notifier);

/// Spell slots to show: the combat summary's, or the sheet's when the server
/// does not send `combat`. Level 0 (pact magic) is excluded.
List<SpellSlot> regularSlots(CharacterDetail character) {
  final source = character.combat.spellSlots.isNotEmpty
      ? character.combat.spellSlots
      : character.spellSlots;
  return [
    for (final s in source)
      if (s.level > 0 && s.max > 0) s,
  ]..sort((a, b) => a.level.compareTo(b.level));
}

/// Pact slots: from the combat summary, or the sheet's level-0 entry.
SpellSlot? pactSlotsOf(CharacterDetail character) {
  final pact = character.combat.pactSlots;
  if (pact != null && pact.max > 0) return pact;
  for (final s in character.spellSlots) {
    if (s.level == 0 && s.max > 0) return s;
  }
  return null;
}

/// Key of the sorcerer's sorcery points: automatic, but Font of Magic turns
/// spell slots into points, so the player may restore them.
const sorceryPointsKey = 'sorcery-points';

/// Key suffix of Tides of Chaos (feature resource of a content pack): a Wild
/// Magic Surge gives its use back, so the player may restore it.
const tidesOfChaosSuffix = 'tides-of-chaos';

/// Whether the acting user may give uses of [resource] back by hand. Automatic
/// class resources (rage, ki, lay on hands...) only come back with rests or by
/// the DM (the server answers 403 to anyone else), except sorcery points and
/// Tides of Chaos.
bool canRestoreResource(CharacterResource resource, {required bool isDm}) =>
    isDm ||
    !resource.isAuto ||
    resource.key == sorceryPointsKey ||
    (resource.key?.endsWith(tidesOfChaosSuffix) ?? false);

/// Resources to show: the combat summary's, or the sheet's as a fallback.
List<CharacterResource> resourcesOf(CharacterDetail character) =>
    character.combat.resources.isNotEmpty ? character.combat.resources : character.resources;

// ---------------------------------------------------------------------------
// Spell slots
// ---------------------------------------------------------------------------

/// One row per slot level with its pips, filled while available: a tap
/// spends a slot, a long press restores one. The pact slots go last.
class SpellSlotsSection extends ConsumerWidget {
  const SpellSlotsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  /// Level 0 stands for the pact slots in the slot endpoints.
  Future<void> _spend(BuildContext context, WidgetRef ref, int level, SpellSlot slot) async {
    if (slot.used >= slot.max) {
      showCombatMessage(
        context,
        level == 0 ? 'No quedan espacios de pacto.' : 'No quedan espacios de nivel $level.',
      );
      return;
    }
    await runCombat(context, () => _controller(ref, character).spendSpellSlot(level));
  }

  Future<void> _restore(BuildContext context, WidgetRef ref, int level, SpellSlot slot) async {
    if (slot.used <= 0) return;
    await runCombat(context, () => _controller(ref, character).restoreSpellSlot(level));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final slots = regularSlots(character);
    final pact = pactSlotsOf(character);
    if (slots.isEmpty && pact == null) return const SizedBox.shrink();

    Widget row(
      String key,
      String label,
      int level,
      SpellSlot slot, {
      String? subtitle,
      Widget? info,
    }) => CombatActionRow(
      key: Key(key),
      title: label,
      cost: '${slot.max - slot.used} / ${slot.max}',
      subtitle: subtitle,
      info: info,
      action: PipRow(
        key: Key('$key-pips'),
        total: slot.max,
        filled: slot.max - slot.used,
        semanticLabel: '$label: ${slot.max - slot.used} de ${slot.max} disponibles',
        onTap: canEdit ? () => _spend(context, ref, level, slot) : null,
        onLongPress: canEdit ? () => _restore(context, ref, level, slot) : null,
      ),
    );

    return CombatGroup(
      key: const Key('combat-slots'),
      title: 'Espacios de conjuro',
      children: [
        for (final slot in slots)
          row('combat-slot-${slot.level}', 'Nivel ${slot.level}', slot.level, slot),
        if (pact != null)
          row(
            'combat-pact-slots',
            pact.level > 0 ? 'Pacto (niv. ${pact.level})' : 'Pacto',
            0,
            pact,
            subtitle: 'Se recuperan con un descanso corto o largo.',
            info: const FeatureInfoButton(index: 'pact-magic'),
          ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Text(
            'Toca para gastar un espacio; mantén pulsado para recuperarlo.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

/// Whether [character] has something for "Recursos": a resource with uses or
/// a once-per-long-rest feature.
bool hasResources(CharacterDetail character) =>
    resourcesOf(character).any((r) => r.max > 0) || character.combat.onceSinceLongRest.isNotEmpty;

/// Every limited use of the character in one list: class resources, racial
/// traits, spells granted with uses per rest, options and feats, items, and
/// the features usable once per long rest.
class ResourcesSection extends ConsumerWidget {
  const ResourcesSection({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;
  final bool canEdit;

  /// A DM or the Owner: may also restore automatic class resources.
  final bool isDm;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resources = resourcesOf(character).where((r) => r.max > 0).toList();
    final once = character.combat.onceSinceLongRest;
    if (resources.isEmpty && once.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (canEdit && resources.any((r) => r.recharge == Recharge.initiative))
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: FilledButton.tonalIcon(
              key: const Key('resources-initiative'),
              onPressed: () => runCombat(
                context,
                () => _controller(ref, character).rechargeOnInitiative(),
                success: 'Recursos recuperados al tirar iniciativa.',
              ),
              icon: const Icon(Icons.bolt_outlined),
              label: const Text('Recuperar al tirar iniciativa'),
            ),
          ),
        CombatGroup(
          key: const Key('combat-resources'),
          title: 'Recursos',
          children: [
            for (final resource in resources)
              ResourceRow(
                key: Key('resource-${resource.id}'),
                character: character,
                resource: resource,
                canEdit: canEdit,
                isDm: isDm,
              ),
            for (final item in once)
              CombatActionRow(
                key: Key('once-${item.key}'),
                title: item.name,
                cost: item.used ? 'Usado' : '1/descanso largo',
                subtitle: 'Una vez por descanso largo',
                action: Pip(
                  filled: !item.used,
                  color: Theme.of(context).colorScheme.primary,
                  size: 20,
                ),
              ),
          ],
        ),
      ],
    );
  }
}

/// Opens where [resource] comes from: the class feature, the spell, the text
/// of a racial trait (the trait itself, never the whole race), or the text of
/// an option or a feat. Null when the resource does not say.
VoidCallback? resourceOriginOpener(
  BuildContext context,
  CharacterDetail character,
  CharacterResource resource,
) {
  final index = resource.sourceIndex;
  final description = resource.sourceDescription;
  void sheet() => showRulesTextSheet(context, resource.name, description ?? 'Sin descripción.');
  switch (resource.sourceKind) {
    case 'feature' when index != null:
      return () => openFeatureDetail(context, index);
    case 'spell' when index != null:
      return () => openSpellDetail(context, index);
    case 'trait':
      // The resource is named after the trait plus its origin ("(raza)" or
      // "(subraza)"); the sheet is titled with the trait alone.
      final trait = resource.name.replaceFirst(RegExp(r' \((raza|subraza)\)$'), '');
      return () => showRulesTextSheet(context, trait, description ?? 'Sin descripción.');
    case 'option' || 'feat':
      return description == null && index == null ? null : sheet;
    default:
      return description == null ? null : sheet;
  }
}

/// Largest resource shown as pips; bigger ones become a pool bar.
const maxPipResource = 10;

/// A resource: name, uses left, its pips (a tap spends one, a long press
/// gives one back when allowed) or a pool bar, what brings it back, where it
/// comes from ([resourceOriginOpener]) and its options, die and recharge roll.
class ResourceRow extends ConsumerWidget {
  const ResourceRow({
    super.key,
    required this.character,
    required this.resource,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;
  final CharacterResource resource;
  final bool canEdit;
  final bool isDm;

  bool get _canRestore => canEdit && canRestoreResource(resource, isDm: isDm);

  Future<void> _spend(BuildContext context, WidgetRef ref, [int amount = 1]) async {
    if (resource.used + amount > resource.max) {
      showCombatMessage(context, 'No quedan usos de ${resource.name}.');
      return;
    }
    await runCombat(
      context,
      () => _controller(ref, character).spendResource(resource.id, amount: amount),
    );
  }

  Future<void> _restore(BuildContext context, WidgetRef ref, [int amount = 1]) async {
    if (resource.used <= 0) return;
    final n = amount > resource.used ? resource.used : amount;
    await runCombat(
      context,
      () => _controller(ref, character).restoreResource(resource.id, amount: n),
    );
  }

  /// Spends one use and rolls the resource's die ([CharacterResource.dice]).
  Future<void> _spendAndRoll(BuildContext context, WidgetRef ref, String dice) async {
    if (resource.used >= resource.max) {
      showCombatMessage(context, 'No quedan usos de ${resource.name}.');
      return;
    }
    final spent = await runCombat(
      context,
      () => _controller(ref, character).spendResource(resource.id, amount: 1),
    );
    if (!spent || !context.mounted) return;
    await rollAndShow(context, '1$dice', label: resource.name);
  }

  /// Asks for the value rolled on the recharge die and sends it.
  Future<void> _rechargeRoll(BuildContext context, WidgetRef ref, RechargeRoll roll) async {
    final value = await promptNumber(
      context,
      title: 'Recarga de ${resource.name}',
      label: 'Resultado del ${roll.dice} (se recupera con ${roll.min} o más)',
      initial: 1,
      min: 1,
      max: roll.faces,
    );
    if (value == null || !context.mounted) return;
    var recharged = false;
    final done = await runCombat(
      context,
      () async => recharged = await _controller(ref, character).rechargeRoll(resource.id, value),
    );
    if (done && context.mounted) {
      showCombatMessage(
        context,
        recharged ? '${resource.name} se recupera.' : '${resource.name} no se recupera esta vez.',
      );
    }
  }

  Future<void> _editPool(BuildContext context, WidgetRef ref) async {
    final remaining = resource.max - resource.used;
    final value = await promptNumber(
      context,
      title: resource.name,
      label: 'Puntos restantes',
      initial: remaining,
      max: _canRestore ? resource.max : remaining,
    );
    if (value == null || value == remaining || !context.mounted) return;
    if (value < remaining) {
      await _spend(context, ref, remaining - value);
    } else if (_canRestore) {
      await _restore(context, ref, value - remaining);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final r = resource;
    final remaining = r.max - r.used;
    final isPool = r.max > maxPipResource;
    final small = theme.textTheme.bodySmall;
    final origin = resourceOriginOpener(context, character, r);
    final dice = r.dice;
    return CombatActionRow(
      title: r.name,
      // A pool shows its count on the bar.
      cost: isPool ? null : '$remaining / ${r.max}',
      costKey: Key('resource-${r.id}-count'),
      subtitle: r.recharge.label,
      info: origin == null
          ? null
          : DetailInfoButton(
              key: Key('resource-${r.id}-info'),
              tooltip: 'Ver origen',
              onPressed: origin,
            ),
      action: isPool
          ? null
          : PipRow(
              key: Key('resource-${r.id}-pips'),
              total: r.max,
              filled: remaining,
              semanticLabel: '${r.name}: $remaining de ${r.max}',
              onTap: canEdit ? () => _spend(context, ref) : null,
              onLongPress: _canRestore ? () => _restore(context, ref) : null,
            ),
      details: [
        if (isPool)
          Row(
            children: [
              IconButton.filledTonal(
                key: Key('resource-${r.id}-minus'),
                tooltip: 'Gastar 1',
                onPressed: canEdit ? () => _spend(context, ref) : null,
                icon: const Icon(Icons.remove),
              ),
              Expanded(
                child: InkWell(
                  key: Key('resource-${r.id}-pool'),
                  onTap: canEdit ? () => _editPool(context, ref) : null,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    child: Column(
                      children: [
                        Text(
                          '$remaining / ${r.max}',
                          style: numericStyle(theme.textTheme.titleMedium),
                        ),
                        const SizedBox(height: 4),
                        LinearProgressIndicator(
                          value: r.max == 0 ? 0 : remaining / r.max,
                          minHeight: 8,
                          borderRadius: BorderRadius.circular(4),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              IconButton.filledTonal(
                key: Key('resource-${r.id}-plus'),
                tooltip: 'Recuperar 1',
                onPressed: _canRestore ? () => _restore(context, ref) : null,
                icon: const Icon(Icons.add),
              ),
            ],
          ),
        for (final option in r.options)
          Padding(
            key: Key('resource-${r.id}-option-${option.index}'),
            padding: const EdgeInsets.only(top: 4),
            child: Row(
              children: [
                Expanded(child: Text(option.name, style: theme.textTheme.bodyMedium)),
                Text(
                  option.label,
                  key: Key('resource-${r.id}-option-${option.index}-cost'),
                  style: small,
                ),
                const SizedBox(width: 8),
                FilledButton.tonal(
                  key: Key('resource-${r.id}-use-${option.index}'),
                  onPressed: canEdit && remaining >= option.amount
                      ? () => _spend(context, ref, option.amount)
                      : null,
                  child: const Text('Usar'),
                ),
              ],
            ),
          ),
        if (r.source != null || r.breakdown != null)
          Row(
            children: [
              StatValue(
                statKey: 'resource-${r.id}-max',
                text: 'Máximo ${r.max}',
                title: r.name,
                breakdown: r.breakdown,
                totalText: '${r.max}',
                style: small,
              ),
              if (r.source != null)
                Expanded(
                  child: Text(
                    ' · ${r.source}',
                    key: Key('resource-${r.id}-source'),
                    style: small,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
          ),
        if (r.rolls.isNotEmpty || r.rollsPending)
          Text(
            r.rollsPending ? 'Tiradas pendientes de anotar' : 'Tiradas: ${r.rolls.join(' · ')}',
            key: Key('resource-${r.id}-rolls'),
            style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
          ),
      ],
      actions: [
        if (dice != null)
          FilledButton.tonalIcon(
            key: Key('resource-${r.id}-roll'),
            onPressed: canEdit && remaining > 0 ? () => _spendAndRoll(context, ref, dice) : null,
            icon: const Icon(Icons.casino_outlined),
            label: Text('Tirar 1$dice'),
          ),
        if (r.rechargeRoll case final roll?)
          OutlinedButton.icon(
            key: Key('resource-${r.id}-recharge-roll'),
            onPressed: canEdit && r.used > 0 ? () => _rechargeRoll(context, ref, roll) : null,
            icon: const Icon(Icons.replay_outlined),
            label: Text('Tirar recarga (${roll.label})'),
          ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Quick consumables
// ---------------------------------------------------------------------------

/// Opens the detail of the inventory entry [itemId] of [characterId] (the id
/// that attacks and quick consumables carry), as the inventory shows it.
Future<void> openCombatItemDetail(
  BuildContext context,
  WidgetRef ref,
  String characterId,
  String itemId,
) async {
  final CharacterItem? item;
  try {
    final inventory = await ref.read(inventoryRepositoryProvider).get(characterId);
    item = inventory.items.where((i) => i.id == itemId).firstOrNull;
  } catch (error) {
    if (context.mounted) showCombatMessage(context, describeCombatError(error));
    return;
  }
  if (!context.mounted) return;
  if (item == null) {
    showCombatMessage(context, 'El objeto ya no está en el inventario.');
    return;
  }
  await openInventoryItemDetail(
    context,
    item,
    campaignId: ref.read(characterControllerProvider(characterId)).value?.campaignId,
  );
}

class ConsumablesSection extends ConsumerWidget {
  const ConsumablesSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  /// Spends one unit through the inventory endpoint, then refreshes the
  /// character (its quick consumables) and the inventory tab.
  Future<void> _use(BuildContext context, WidgetRef ref, QuickConsumable item) async {
    final inventory = ref.read(inventoryRepositoryProvider);
    await runCombat(context, () async {
      await inventory.use(character.id, item.itemId);
      if (context.mounted) ref.invalidate(inventoryControllerProvider(character.id));
      await ref.read(characterControllerProvider(character.id).notifier).reload();
    }, success: 'Usado: ${item.name}.');
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = character.combat.quickConsumables;
    if (items.isEmpty) return const SizedBox.shrink();
    return CombatGroup(
      key: const Key('combat-consumables'),
      title: 'Consumibles rápidos',
      children: [
        for (final item in items)
          CombatActionRow(
            key: Key('consumable-${item.itemId}'),
            icon: AppIcons.drop,
            title: item.name,
            subtitle: item.charges == null
                ? 'Cantidad: ${item.quantity}'
                : 'Cantidad: ${item.quantity} · Cargas: ${item.charges}',
            info: DetailInfoButton(
              key: Key('detail-item-${item.itemId}'),
              onPressed: () => openCombatItemDetail(context, ref, character.id, item.itemId),
            ),
            action: FilledButton.tonal(
              key: Key('consumable-use-${item.itemId}'),
              onPressed: canEdit ? () => _use(context, ref, item) : null,
              child: const Text('Usar'),
            ),
          ),
      ],
    );
  }
}
