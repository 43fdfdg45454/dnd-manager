import '../../catalog/data/models.dart' show ItemModifier;
import '../../catalog/domain/item_modifier_format.dart';
import '../../items/data/models.dart' show ItemOverrides;
import '../../items/domain/items_format.dart';
import '../data/models.dart';
import 'character_format.dart';
import 'payload_format.dart';

/// One field of a sheet edit: what it was and what it would become.
typedef FieldChange = ({String label, String? before, String after});

/// Readable detail of a change request, shaped by its type. The UI renders
/// each variant differently (a table for sheet edits, a card for an item...).
sealed class ChangeDetail {
  const ChangeDetail();
}

/// Sheet edit: one row per changed field, with the previous value when the
/// server stored a snapshot (null for requests older than that feature).
final class SheetChangeDetail extends ChangeDetail {
  const SheetChangeDetail(this.fields);

  final List<FieldChange> fields;
}

/// An item to add: its resolved name and what defines it.
final class ItemChangeDetail extends ChangeDetail {
  const ItemChangeDetail({
    required this.name,
    required this.quantity,
    required this.custom,
    required this.lines,
    required this.modifiers,
  });

  final String name;
  final int quantity;

  /// True for a custom item or a catalog item changed by hand.
  final bool custom;

  /// "label: value" facts (category, damage, armor, properties...).
  final List<PayloadLine> lines;

  /// Readable modifiers ("+2 Fuerza", "+1 CA").
  final List<String> modifiers;
}

/// Removing [quantity] of an item; [had] and [left] come from the snapshot.
final class RemoveItemDetail extends ChangeDetail {
  const RemoveItemDetail({required this.name, required this.quantity, this.had});

  final String name;
  final int quantity;
  final int? had;

  int? get left => had == null ? null : had! - quantity;
}

/// Money change with the balance before and after when known.
final class MoneyChangeDetail extends ChangeDetail {
  const MoneyChangeDetail({required this.deltaCp, this.beforeCp, this.reason});

  final int deltaCp;
  final int? beforeCp;
  final String? reason;

  int? get afterCp => beforeCp == null ? null : beforeCp! + deltaCp;
}

/// Nothing to detail (activation, unknown types).
final class PlainChangeDetail extends ChangeDetail {
  const PlainChangeDetail(this.lines);

  final List<PayloadLine> lines;
}

/// Builds the detail of [request] from its payload and snapshot.
ChangeDetail describeChange(ChangeRequest request) {
  final payload = request.payload;
  final before = request.before;
  switch (request.type) {
    case ChangeRequestType.editSheet:
      return SheetChangeDetail(_sheetFields(payload, before));
    case ChangeRequestType.addItem || ChangeRequestType.customItem:
      return _itemDetail(payload, before);
    case ChangeRequestType.removeItem:
      return RemoveItemDetail(
        name: '${payload['itemName'] ?? 'Objeto'}',
        quantity: _int(payload['quantity']) ?? 1,
        had: _int(before?['quantity']),
      );
    case ChangeRequestType.adjustMoney:
      return MoneyChangeDetail(
        deltaCp: _int(payload['deltaCp']) ?? 0,
        beforeCp: _int(before?['copperPieces']),
        reason: payload['reason'] is String && '${payload['reason']}'.isNotEmpty
            ? '${payload['reason']}'
            : null,
      );
    case ChangeRequestType.activate || ChangeRequestType.other:
      return PlainChangeDetail(describePayload(payload));
  }
}

List<FieldChange> _sheetFields(Map<String, dynamic> payload, Map<String, dynamic>? before) {
  final after = describePayload(payload);
  final previous = before == null ? const <PayloadLine>[] : describePayload(before);
  final previousByLabel = {for (final l in previous) l.label: l.value};
  final fields = <FieldChange>[];
  for (final line in after) {
    if (line.label == 'Puntuaciones base') {
      fields.addAll(_abilityFields(payload['baseAbilities'], before?['baseAbilities']));
      continue;
    }
    fields.add((label: line.label, before: previousByLabel[line.label], after: line.value));
  }
  return fields;
}

/// One row per ability, showing score and modifier, so "Fuerza: 15 (+2) → 17 (+3)".
List<FieldChange> _abilityFields(Object? after, Object? before) {
  if (after is! Map) return const [];
  String text(Object? score) {
    final value = _int(score);
    if (value == null) return '—';
    return '$value (${formatModifier(abilityModifierOf(value))})';
  }

  return [
    for (final e in after.entries)
      (
        label: abilityLabel('${e.key}'),
        before: before is Map && before.containsKey(e.key) ? text(before[e.key]) : null,
        after: text(e.value),
      ),
  ];
}

ItemChangeDetail _itemDetail(Map<String, dynamic> payload, Map<String, dynamic>? before) {
  final overrides = ItemOverrides.fromJson(payload['overrides']);
  final template = before?['template'] is Map ? Map<String, dynamic>.from(before!['template'] as Map) : null;
  final name = overrides.name ?? (template?['name'] as String?) ?? 'Objeto';
  final lines = <PayloadLine>[];
  void add(String label, Object? value) {
    if (value == null) return;
    final text = value is List ? value.join(', ') : '$value';
    if (text.isEmpty) return;
    lines.add((label: label, value: text));
  }

  final templateDamage = template?['damage'] is Map ? template!['damage'] as Map : null;
  final templateArmor = template?['armor'] is Map ? template!['armor'] as Map : null;
  if (template != null) add('Objeto del catálogo', template['name']);
  add('Categoría', overrides.category ?? template?['category']);
  final damageDice = overrides.damageDice ?? templateDamage?['dice'];
  if (damageDice != null) {
    final type = overrides.damageType ?? _nameOf(templateDamage?['type']);
    add('Daño', type == null ? damageDice : '$damageDice ${damageTypeLabel(type)}');
  }
  final armorBase = overrides.armorClassBase ?? templateArmor?['base'] ?? templateArmor?['armorClassBase'];
  if (armorBase != null) add('CA', armorBase);
  add('Propiedades', overrides.properties ?? template?['properties']);
  if (overrides.attackBonus != null) add('Bonificador de ataque', formatModifier(overrides.attackBonus!));
  if (overrides.damageBonus != null) add('Bonificador de daño', formatModifier(overrides.damageBonus!));
  if (overrides.requiresAttunement ?? (template?['requiresAttunement'] == true)) {
    add('Sintonización', 'Requiere sintonización');
  }
  final weight = overrides.weightLb ?? template?['weightLb'];
  if (weight is num) add('Peso', '${formatPlainNumber(weight.toDouble())} lb');
  final cost = _int(template?['costCp']);
  if (cost != null) add('Precio de catálogo', formatMoney(cost));
  final description = overrides.description ?? _stringList(template?['description']);
  if (description != null && description.isNotEmpty) add('Descripción', description.join(' '));
  final effects = overrides.effects;
  if (effects != null && effects.isNotEmpty) add('Efectos', effects);

  final modifiers =
      overrides.modifiers ?? (template == null ? const <ItemModifier>[] : ItemModifier.listFromJson(template['modifiers']));
  return ItemChangeDetail(
    name: name,
    quantity: _int(payload['quantity']) ?? 1,
    custom: payload['templateId'] == null || !overrides.isEmpty,
    lines: lines,
    modifiers: [for (final m in modifiers) describeItemModifier(m)],
  );
}

int? _int(Object? value) => value is num ? value.toInt() : int.tryParse('$value');

String? _nameOf(Object? value) => switch (value) {
  final String s => s,
  final Map m => m['name'] as String? ?? m['index'] as String?,
  _ => null,
};

List<String>? _stringList(Object? value) =>
    value is List ? [for (final v in value) '$v'] : value is String ? [value] : null;

/// "Fuerza", "Destreza"... from a short key.
String abilityLabel(String key) => abilityName(key);
