import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/components.dart';
import '../../../../core/ui/stat_value.dart';
import '../../../dice/ui/dice_sheet.dart';
import '../../../items/data/inventory_repository.dart';
import '../../../items/data/items_controllers.dart';
import '../../../items/data/models.dart' show CharacterItem;
import '../../../items/ui/effective_item_page.dart' show openInventoryItemDetail;
import '../../../catalog/ui/catalog_detail_links.dart' show DetailInfoButton;
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import 'combat_support.dart';

CharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

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

/// A row per slot level with filled (available) and empty (used) dots. Tap
/// spends a slot, long press restores one. Pact magic is listed apart.
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
    final theme = Theme.of(context);

    Widget row(String key, String label, int level, SpellSlot slot) => Row(
      key: Key(key),
      children: [
        SizedBox(
          width: 112,
          child: Text(
            label,
            style: theme.textTheme.bodyLarge,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        Expanded(
          child: Align(
            alignment: AlignmentDirectional.centerStart,
            child: PipRow(
              key: Key('$key-pips'),
              total: slot.max,
              filled: slot.max - slot.used,
              semanticLabel: '$label: ${slot.max - slot.used} de ${slot.max} disponibles',
              onTap: canEdit ? () => _spend(context, ref, level, slot) : null,
              onLongPress: canEdit ? () => _restore(context, ref, level, slot) : null,
            ),
          ),
        ),
      ],
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Espacios de conjuro', padding: combatSectionPadding),
        CombatCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final slot in slots)
                row('combat-slot-${slot.level}', 'Nivel ${slot.level}', slot.level, slot),
              if (pact != null)
                row(
                  'combat-pact-slots',
                  pact.level > 0 ? 'Pacto (niv. ${pact.level})' : 'Pacto',
                  0,
                  pact,
                ),
              Text(
                'Toca para gastar un espacio; mantén pulsado para recuperarlo.',
                style: theme.textTheme.bodySmall,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

/// Class and item resources: dots up to 10 uses, a pool bar beyond that.
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
        const SectionHeader('Recursos', padding: combatSectionPadding),
        for (final resource in resources)
          ResourceTile(
            key: Key('resource-${resource.id}'),
            character: character,
            resource: resource,
            canEdit: canEdit,
            isDm: isDm,
          ),
        if (once.isNotEmpty)
          CombatCard(
            title: 'Una vez por descanso largo',
            child: Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final item in once)
                  Chip(
                    key: Key('once-${item.key}'),
                    avatar: Pip(
                      filled: !item.used,
                      color: Theme.of(context).colorScheme.primary,
                      size: 18,
                    ),
                    label: Text(item.used ? '${item.name} (usado)' : item.name),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Largest resource shown as dots; bigger ones become a pool bar.
const maxPipResource = 10;

class ResourceTile extends ConsumerWidget {
  const ResourceTile({
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
    final body = isPool
        ? Row(
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
                          style: numericStyle(theme.textTheme.titleLarge),
                        ),
                        const SizedBox(height: 4),
                        LinearProgressIndicator(
                          value: r.max == 0 ? 0 : remaining / r.max,
                          minHeight: 10,
                          borderRadius: BorderRadius.circular(5),
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
          )
        : PipRow(
            key: Key('resource-${r.id}-pips'),
            total: r.max,
            filled: remaining,
            semanticLabel: '${r.name}: $remaining de ${r.max}',
            onTap: canEdit ? () => _spend(context, ref) : null,
            onLongPress: _canRestore ? () => _restore(context, ref) : null,
          );
    final dice = r.dice;
    final details = <Widget>[
      for (final option in r.options)
        Padding(
          key: Key('resource-${r.id}-option-${option.index}'),
          padding: const EdgeInsets.only(top: 6),
          child: Row(
            children: [
              Expanded(child: Text(option.name, style: theme.textTheme.bodyMedium)),
              Text(
                option.label,
                key: Key('resource-${r.id}-option-${option.index}-cost'),
                style: theme.textTheme.bodySmall,
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
      if (dice != null)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: FilledButton.tonalIcon(
            key: Key('resource-${r.id}-roll'),
            onPressed: canEdit && remaining > 0 ? () => _spendAndRoll(context, ref, dice) : null,
            icon: const Icon(Icons.casino_outlined),
            label: Text('Tirar 1$dice'),
          ),
        ),
      if (r.source != null || r.breakdown != null)
        Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Row(
            children: [
              StatValue(
                statKey: 'resource-${r.id}-max',
                text: 'Máximo ${r.max}',
                title: r.name,
                breakdown: r.breakdown,
                totalText: '${r.max}',
                style: theme.textTheme.bodySmall,
              ),
              if (r.source != null)
                Expanded(
                  child: Text(
                    ' · ${r.source}',
                    key: Key('resource-${r.id}-source'),
                    style: theme.textTheme.bodySmall,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
          ),
        ),
    ];
    return CombatCard(
      title: r.name,
      trailing: Text(r.recharge.label, style: theme.textTheme.bodySmall),
      child: r.rolls.isEmpty && !r.rollsPending && details.isEmpty
          ? body
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                body,
                ...details,
                if (r.rolls.isNotEmpty || r.rollsPending)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      r.rollsPending
                          ? 'Tiradas pendientes de anotar'
                          : 'Tiradas: ${r.rolls.join(' · ')}',
                      key: Key('resource-${r.id}-rolls'),
                      style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
              ],
            ),
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
  await openInventoryItemDetail(context, item);
}

class ConsumablesSection extends ConsumerWidget {
  const ConsumablesSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  /// Spends one unit through the inventory endpoint, then refreshes the
  /// character (its quick consumables) and the inventory tab.
  Future<void> _use(BuildContext context, WidgetRef ref, QuickConsumable item) async {
    final inventory = ref.read(inventoryRepositoryProvider);
    final controller = _controller(ref, character);
    await runCombat(context, () async {
      await inventory.use(character.id, item.itemId);
      if (context.mounted) ref.invalidate(inventoryControllerProvider(character.id));
      await controller.reload();
    }, success: 'Usado: ${item.name}.');
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = character.combat.quickConsumables;
    if (items.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Consumibles rápidos', padding: combatSectionPadding),
        CombatCard(
          child: Column(
            children: [
              for (final item in items)
                ListTile(
                  key: Key('consumable-${item.itemId}'),
                  contentPadding: EdgeInsets.zero,
                  title: Text(item.name),
                  subtitle: Text(
                    item.charges == null
                        ? 'Cantidad: ${item.quantity}'
                        : 'Cantidad: ${item.quantity} · Cargas: ${item.charges}',
                  ),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      DetailInfoButton(
                        key: Key('detail-item-${item.itemId}'),
                        onPressed: () =>
                            openCombatItemDetail(context, ref, character.id, item.itemId),
                      ),
                      const SizedBox(width: 4),
                      FilledButton.tonal(
                        key: Key('consumable-use-${item.itemId}'),
                        onPressed: canEdit ? () => _use(context, ref, item) : null,
                        child: const Text('Usar'),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
