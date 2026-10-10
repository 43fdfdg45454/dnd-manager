import '../../catalog/data/models.dart' show ItemModifier;
import '../../catalog/domain/item_modifier_format.dart';
import '../../items/data/models.dart' show ItemOverrides;
import '../../items/domain/items_format.dart';
import '../../../core/characters/change_detail.dart';
import '../data/models.dart';
import 'character_format.dart';
import 'payload_format.dart';

export '../../../core/characters/change_detail.dart';

/// Sheet edit: one row per changed field, with the previous value when the
/// server stored a snapshot (null for requests older than that feature).
final class SheetChangeDetail extends ChangeDetail {
  const SheetChangeDetail(this.fields);

  final List<FieldChange> fields;
}

/// Builds the detail of [request]: the D&D 5e types first
/// ([describeDnd5eChange]) and then the ones of the core.
ChangeDetail describeChange(ChangeRequest request) =>
    describeDnd5eChange(request) ?? describeCoreChange(request)!;

/// The detail of the request types D&D 5e describes (sheet edits, companion,
/// items to add, activation and other), or null for the ones of the core.
ChangeDetail? describeDnd5eChange(ChangeRequest request) {
  final payload = request.payload;
  final before = request.before;
  return switch (request.type) {
    ChangeRequestType.editSheet => SheetChangeDetail(_sheetFields(payload, before)),
    ChangeRequestType.addItem || ChangeRequestType.customItem => _itemDetail(payload, before),
    ChangeRequestType.companion => SheetChangeDetail(_companionFields(payload, before)),
    ChangeRequestType.activate || ChangeRequestType.other => PlainChangeDetail(
      describePayload(payload),
    ),
    ChangeRequestType.removeItem || ChangeRequestType.adjustMoney => null,
  };
}

/// "Bestia: Wolf → Panther" and "Nombre: Ceniza → Sombra".
List<FieldChange> _companionFields(Map<String, dynamic> payload, Map<String, dynamic>? before) {
  String? text(Object? value) => value is String && value.isNotEmpty ? value : null;
  return [
    (
      label: 'Bestia',
      before: text(before?['beastName']) ?? text(before?['beastIndex']),
      after: text(payload['beastName']) ?? text(payload['beastIndex']) ?? '—',
    ),
    (label: 'Nombre', before: text(before?['name']), after: text(payload['name']) ?? '—'),
  ];
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
