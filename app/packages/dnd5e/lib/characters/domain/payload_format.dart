import 'package:opentrpg_core/core/characters/change_detail.dart' show PayloadLine;

import '../../catalog/data/models.dart' show titleFromIndex;
import 'character_format.dart';

export 'package:opentrpg_core/core/characters/change_detail.dart' show PayloadLine;

const _fieldLabels = <String, String>{
  'name': 'Nombre',
  'raceIndex': 'Raza',
  'subraceIndex': 'Subraza',
  'backgroundIndex': 'Trasfondo',
  'alignment': 'Alineamiento',
  'applyRacialBonuses': 'Aplicar bonos raciales',
  'hpMode': 'Modo de puntos de golpe',
  'baseAbilities': 'Puntuaciones base',
  'classes': 'Clases',
  'proficiencies': 'Competencias',
  'spells': 'Hechizos',
  'overrides': 'Valores modificados',
  'notes': 'Notas',
  'backstory': 'Trasfondo (historia)',
  'personalityTraits': 'Rasgos de personalidad',
  'ideals': 'Ideal',
  'bonds': 'Vínculo',
  'flaws': 'Defecto',
  'backgroundDetail': 'Detalle del trasfondo',
  'heightInches': 'Altura (pulgadas)',
  'weightPounds': 'Peso (libras)',
  'copperPieces': 'Dinero',
};

/// Turns a change-request payload into "label -> value" lines a person can read.
List<PayloadLine> describePayload(Map<String, dynamic> payload) => [
  for (final e in payload.entries)
    (label: _fieldLabels[e.key] ?? e.key, value: _format(e.key, e.value)),
];

String _format(String key, Object? value) {
  if (value == null) return '—';
  switch (key) {
    case 'copperPieces':
      if (value is num) return '${copperToGoldText(value.toInt())} gp';
    case 'baseAbilities':
      if (value is Map) {
        return value.entries.map((e) => '${abilityAbbreviation('${e.key}')} ${e.value}').join(', ');
      }
    case 'classes':
      if (value is List) {
        return value.isEmpty ? 'Ninguna' : value.map(_classText).join(', ');
      }
    case 'proficiencies':
      if (value is List) {
        return value.isEmpty ? 'Ninguna' : value.map(_proficiencyText).join(', ');
      }
    case 'spells':
      if (value is List) {
        return value.isEmpty ? 'Ninguno' : value.map(_spellText).join(', ');
      }
    case 'overrides':
      if (value is List) {
        return value.isEmpty ? 'Ninguno' : value.map(_overrideText).join('; ');
      }
    case 'raceIndex' || 'subraceIndex' || 'backgroundIndex':
      if (value is String) return titleFromIndex(value);
    case 'alignment':
      if (value is String) return alignmentLabel(value);
    case 'hpMode':
      if (value == 'Average') return 'Promedio';
      if (value == 'Manual') return 'Manual';
  }
  return _generic(value);
}

String _generic(Object? value) {
  if (value == null) return '—';
  if (value is bool) return value ? 'Sí' : 'No';
  if (value is List) return value.isEmpty ? '—' : value.map(_generic).join(', ');
  if (value is Map) {
    return value.entries.map((e) => '${e.key}: ${_generic(e.value)}').join(', ');
  }
  return '$value';
}

String _classText(Object? e) {
  if (e is! Map) return _generic(e);
  final sub = e['subclassIndex'];
  final base = '${titleFromIndex('${e['classIndex']}')} ${e['level']}';
  return sub == null ? base : '$base (${titleFromIndex('$sub')})';
}

String _proficiencyText(Object? e) {
  if (e is! Map) return _generic(e);
  final key = '${e['key']}';
  final name = e['type'] == 'Skill' ? skillLabel(key, titleFromIndex(key)) : key;
  return e['expertise'] == true ? '$name (pericia)' : name;
}

String _spellText(Object? e) {
  if (e is! Map) return _generic(e);
  final name = titleFromIndex('${e['spellIndex']}');
  return e['isPrepared'] == true ? '$name (preparado)' : name;
}

String _overrideText(Object? e) {
  if (e is! Map) return _generic(e);
  final text = '${overrideFieldLabel('${e['field']}')}: ${e['value']}';
  final note = e['note'];
  return note == null ? text : '$text ($note)';
}
