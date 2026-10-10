import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Every `featureIndex` of the class panels names a real entry of the SRD
/// features dataset of the server (never an invented index).
void main() {
  test('los índices de rasgo de los paneles existen en el SRD', () {
    final dataset = File('../server/seed/srd/5e-SRD-Features.json');
    expect(dataset.existsSync(), isTrue, reason: 'Falta ${dataset.path}');
    final known = {
      for (final f in jsonDecode(dataset.readAsStringSync()) as List) (f as Map)['index'] as String,
    };

    final pattern = RegExp(r"featureIndex: '([^']+)'");
    final used = <String>{};
    for (final file in Directory('lib/features/characters/ui/combat/panels').listSync()) {
      if (file is! File || !file.path.endsWith('.dart')) continue;
      for (final m in pattern.allMatches(file.readAsStringSync())) {
        used.add(m.group(1)!);
      }
    }

    expect(used, isNotEmpty);
    expect(used.difference(known), isEmpty);
  });
}
