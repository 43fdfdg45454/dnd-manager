import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

/// Opens a tab of the combat view: "actions", "spells", "resources" or "more".
Future<void> openCombatSection(WidgetTester tester, String section) async {
  await tester.tap(find.byKey(Key('combat-tab-$section')));
  await tester.pumpAndSettle();
}
