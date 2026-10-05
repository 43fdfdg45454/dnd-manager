import 'package:dnd_companion/features/home/data/server_info.dart';
import 'package:dnd_companion/features/home/data/server_info_repository.dart';
import 'package:dnd_companion/features/home/ui/home_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('muestra el nombre y la versión del servidor', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          serverInfoProvider.overrideWith(
            (ref) async => const ServerInfo(name: 'dnd-companion-api', version: '0.1.0'),
          ),
        ],
        child: const MaterialApp(home: HomePage()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Conectado a dnd-companion-api v0.1.0'), findsOneWidget);
  });

  testWidgets('muestra error y botón de reintento si el servidor no responde', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          serverInfoProvider.overrideWith((ref) async => throw Exception('sin red')),
        ],
        child: const MaterialApp(home: HomePage()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
    expect(find.text('Reintentar'), findsOneWidget);
  });
}
