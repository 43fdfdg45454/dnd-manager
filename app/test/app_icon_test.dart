import 'dart:io';

import 'package:dnd_companion/core/theme/app_icon.dart';
import 'package:dnd_companion/core/theme/icons.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _host(Widget child) => MaterialApp(
  home: Scaffold(body: Center(child: child)),
);

/// Bounding box of the absolute M/L/H/V commands of every path of an icon.
Rect _pathBounds(String svg) {
  final xs = <double>[];
  final ys = <double>[];
  for (final d in RegExp(r' d="([^"]*)"').allMatches(svg).map((m) => m.group(1)!)) {
    var x = 0.0;
    var y = 0.0;
    for (final cmd in RegExp(r'([MLHVZ])([^MLHVZ]*)').allMatches(d)) {
      final nums = RegExp(r'-?\d*\.?\d+')
          .allMatches(cmd.group(2)!)
          .map((m) => double.parse(m.group(0)!))
          .toList();
      switch (cmd.group(1)) {
        case 'M' || 'L':
          for (var i = 0; i + 1 < nums.length; i += 2) {
            x = nums[i];
            y = nums[i + 1];
            xs.add(x);
            ys.add(y);
          }
        case 'H':
          for (final v in nums) {
            x = v;
            xs.add(x);
            ys.add(y);
          }
        case 'V':
          for (final v in nums) {
            y = v;
            xs.add(x);
            ys.add(y);
          }
      }
    }
  }
  xs.sort();
  ys.sort();
  return Rect.fromLTRB(xs.first, ys.first, xs.last, ys.last);
}

void main() {
  testWidgets('AppIcon mide 24 por defecto y toma el tamaño del IconTheme', (tester) async {
    await tester.pumpWidget(_host(const AppIcon(AppIcons.d20, key: Key('plain'))));
    expect(tester.getSize(find.byKey(const Key('plain'))), const Size.square(24));

    await tester.pumpWidget(
      _host(
        const IconTheme(
          data: IconThemeData(size: 18),
          child: AppIcon(AppIcons.d20, key: Key('themed')),
        ),
      ),
    );
    expect(tester.getSize(find.byKey(const Key('themed'))), const Size.square(18));

    await tester.pumpWidget(
      _host(
        const IconTheme(
          data: IconThemeData(size: 18),
          child: AppIcon(AppIcons.d20, key: Key('explicit'), size: 32),
        ),
      ),
    );
    expect(tester.getSize(find.byKey(const Key('explicit'))), const Size.square(32));
  });

  testWidgets('AppIcon se alinea con un Icon de Material en una fila y en un botón', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                AppIcon(AppIcons.coins, key: Key('row-app')),
                Icon(Icons.star, key: Key('row-material')),
                Text('Texto'),
              ],
            ),
            FilledButton.icon(
              onPressed: () {},
              icon: const AppIcon(AppIcons.treasure, key: Key('button-app')),
              label: const Text('Repartir'),
            ),
          ],
        ),
      ),
    );

    final app = tester.getRect(find.byKey(const Key('row-app')));
    final material = tester.getRect(find.byKey(const Key('row-material')));
    expect(app.size, material.size);
    expect(app.center.dy, material.center.dy);
    // Buttons give their icons an 18 px IconTheme: the AppIcon follows it.
    expect(tester.getSize(find.byKey(const Key('button-app'))), const Size.square(18));
  });

  test('los SVG de los iconos usan viewBox 24×24 y están centrados', () {
    for (final icon in AppIcons.values) {
      final svg = File(icon.assetPath).readAsStringSync();
      expect(svg, contains('viewBox="0 0 24 24"'), reason: icon.name);
      final bounds = _pathBounds(svg);
      expect(bounds.left, greaterThanOrEqualTo(1), reason: icon.name);
      expect(bounds.top, greaterThanOrEqualTo(1), reason: icon.name);
      expect(bounds.right, lessThanOrEqualTo(23), reason: icon.name);
      expect(bounds.bottom, lessThanOrEqualTo(23), reason: icon.name);
      expect((bounds.center.dx - 12).abs(), lessThanOrEqualTo(0.75), reason: '${icon.name} x');
      expect((bounds.center.dy - 12).abs(), lessThanOrEqualTo(0.75), reason: '${icon.name} y');
    }
  });
}
