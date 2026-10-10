import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Layers of the app (phase 33): no file of the **core** imports a file of
/// the **D&D 5e** module. The 5e set is listed here by folder and file; every
/// other file under `lib/` is core, except the host (`main.dart`, which
/// registers the systems), with no exceptions.

/// Folders of the 5e module (paths under `lib/`).
const _dnd5eFolders = [
  'systems/dnd5e/',
  'features/catalog/',
  'features/characters/ui/combat/',
  'features/characters/ui/level_up/',
  'features/characters/ui/wizard/',
];

/// Single 5e files outside those folders.
const _dnd5eFiles = {
  'features/characters/data/models.dart', // transitional barrel of the 5e models
  'features/characters/data/companion_models.dart',
  'features/characters/data/character_wizard_controller.dart',
  'features/characters/data/level_up_controller.dart',
  'features/characters/domain/change_details.dart',
  'features/characters/domain/character_format.dart',
  'features/characters/domain/class_theme.dart',
  'features/characters/domain/combat_math.dart',
  'features/characters/domain/height_weight.dart',
  'features/characters/domain/payload_format.dart',
  'features/characters/domain/spell_combat.dart',
  'features/characters/ui/character_tabs.dart',
  'features/characters/ui/invalid_choices_page.dart',
  'features/characters/ui/origin_choices_widgets.dart',
  'features/characters/ui/point_buy_dialog.dart',
  'features/characters/ui/prepare_spells_page.dart',
  'features/characters/ui/rest_rolls_page.dart',
  'features/characters/ui/skill_rolls.dart',
  'features/characters/ui/spell_picker_page.dart',
  'features/items/domain/combat_usable.dart',
  'features/items/domain/item_form_data.dart',
  'features/characters/ui/sheet_editor_form.dart',
  'features/items/ui/attunement_dialog.dart',
  'features/items/ui/item_fields_form.dart',
  'features/session/data/party_repository.dart',
  'features/session/ui/dm/dm_character_sheet.dart',
  'features/session/ui/dm/party_roster.dart',
  'features/session/ui/player/combat_items_section.dart',
};

/// The host: registers the systems (`gameSystemsProvider`).
const _host = {'main.dart'};

bool _isDnd5e(String path) => _dnd5eFiles.contains(path) || _dnd5eFolders.any(path.startsWith);

final _directive = RegExp(r"^\s*(?:import|export)\s+'([^']+)'", multiLine: true);

/// Normalizes `a/b/../c/./d.dart` to `a/c/d.dart`.
String _normalize(String path) {
  final parts = <String>[];
  for (final part in path.split('/')) {
    if (part == '..') {
      if (parts.isNotEmpty) parts.removeLast();
    } else if (part != '.' && part.isNotEmpty) {
      parts.add(part);
    }
  }
  return parts.join('/');
}

/// The files under `lib/` that [path] imports or exports, as paths under
/// `lib/`.
List<String> _importsOf(String path, String source) {
  final dir = path.contains('/') ? path.substring(0, path.lastIndexOf('/')) : '';
  return [
    for (final match in _directive.allMatches(source))
      if (match.group(1)! case final uri when uri.startsWith('package:opentrpg/'))
        uri.substring('package:opentrpg/'.length)
      else if (match.group(1)! case final uri
          when !uri.startsWith('package:') && !uri.startsWith('dart:'))
        _normalize(dir.isEmpty ? uri : '$dir/$uri'),
  ];
}

void main() {
  final lib = Directory('lib');
  final files = {
    for (final entity in lib.listSync(recursive: true))
      if (entity is File && entity.path.endsWith('.dart'))
        entity.path.replaceAll(r'\', '/').substring('lib/'.length): entity,
  };

  Map<String, List<String>> violations() => {
    for (final MapEntry(key: path, value: file) in files.entries)
      if (!_isDnd5e(path) && !_host.contains(path))
        path: [
          for (final target in _importsOf(path, file.readAsStringSync()))
            if (_isDnd5e(target)) target,
        ],
  }..removeWhere((_, targets) => targets.isEmpty);

  test('ningún fichero del núcleo importa un fichero 5e', () {
    expect(violations(), isEmpty);
  });

  test('los ficheros 5e enumerados existen', () {
    for (final path in _dnd5eFiles) {
      expect(files.containsKey(path), isTrue, reason: '$path no existe');
    }
    for (final folder in _dnd5eFolders) {
      expect(files.keys.any((p) => p.startsWith(folder)), isTrue, reason: '$folder está vacía');
    }
  });

  test('el test reconoce una importación prohibida', () {
    expect(_importsOf('core/x/y.dart', "import '../../systems/dnd5e/dnd5e_ui.dart';\n"), [
      'systems/dnd5e/dnd5e_ui.dart',
    ]);
    expect(_isDnd5e('systems/dnd5e/dnd5e_ui.dart'), isTrue);
    expect(_isDnd5e('core/systems/game_system_ui.dart'), isFalse);
  });
}
