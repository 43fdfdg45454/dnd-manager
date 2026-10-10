import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Layers of the app (phase 33): the host `opentrpg` (`lib/`) only boots the
/// app and registers the systems; the core `opentrpg_core`
/// (`packages/core`) never reaches the D&D 5e module `opentrpg_dnd5e`
/// (`packages/dnd5e`), and no package reaches into another one with a
/// relative path that leaves its own `lib/`.

final _directive = RegExp(r"^\s*(?:import|export|part)\s+'([^']+)'", multiLine: true);

/// Normalizes `a/b/../c/./d.dart` to `a/c/d.dart`; a path that climbs above
/// its root keeps a leading `..`.
String _normalize(String path) {
  final parts = <String>[];
  for (final part in path.split('/')) {
    if (part == '..') {
      if (parts.isNotEmpty && parts.last != '..') {
        parts.removeLast();
      } else {
        parts.add('..');
      }
    } else if (part != '.' && part.isNotEmpty) {
      parts.add(part);
    }
  }
  return parts.join('/');
}

/// The URIs [path] (relative to its package's `lib/`) imports, exports or
/// includes as a part, with relative ones resolved against that `lib/`.
List<String> _importsOf(String path, String source) {
  final dir = path.contains('/') ? path.substring(0, path.lastIndexOf('/')) : '';
  return [
    for (final match in _directive.allMatches(source))
      if (match.group(1)! case final uri when uri.startsWith('package:') || uri.startsWith('dart:'))
        uri
      else
        _normalize(dir.isEmpty ? match.group(1)! : '$dir/${match.group(1)!}'),
  ];
}

/// Dart files under [root], keyed by their path relative to it.
Map<String, String> _sources(String root) => {
  for (final entity in Directory(root).listSync(recursive: true))
    if (entity is File && entity.path.endsWith('.dart'))
      entity.path.replaceAll(r'\', '/').substring(root.length + 1): entity.readAsStringSync(),
};

/// Imports of the package at [root] that point to a package in [forbidden]
/// or climb out of its `lib/`.
Map<String, List<String>> _violations(String root, List<String> forbidden) => {
  for (final MapEntry(key: path, value: source) in _sources(root).entries)
    path: [
      for (final uri in _importsOf(path, source))
        if (uri.startsWith('..') || forbidden.any((p) => uri.startsWith('package:$p/'))) uri,
    ],
}..removeWhere((_, uris) => uris.isEmpty);

void main() {
  test('ningún fichero del núcleo importa un fichero 5e', () {
    expect(_violations('packages/core/lib', ['opentrpg', 'opentrpg_dnd5e']), isEmpty);
    expect(
      File('packages/core/pubspec.yaml').readAsStringSync(),
      isNot(contains('opentrpg_dnd5e')),
    );
  });

  test('el módulo 5e no importa el anfitrión', () {
    expect(_violations('packages/dnd5e/lib', ['opentrpg']), isEmpty);
  });

  test('el anfitrión solo arranca la app', () {
    expect(_sources('lib').keys.toSet(), {'main.dart', 'app.dart'});
  });

  test('el test reconoce una importación prohibida', () {
    expect(_importsOf('core/x/y.dart', "import '../../../../dnd5e/lib/dnd5e_ui.dart';\n"), [
      '../../dnd5e/lib/dnd5e_ui.dart',
    ]);
    expect(_importsOf('core/x/y.dart', "import 'package:opentrpg_dnd5e/dnd5e_ui.dart';\n"), [
      'package:opentrpg_dnd5e/dnd5e_ui.dart',
    ]);
    expect(_importsOf('core/x/y.dart', "import '../z.dart';\n"), ['core/z.dart']);
  });
}
