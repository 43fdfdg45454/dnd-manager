import 'package:flutter/material.dart';

import 'dice_sheet.dart';

/// "Dados" tab of the app: the dice tray as a full page (quick dice, expression,
/// favorites and history), the same one the sheets open as a bottom sheet.
class DicePage extends StatelessWidget {
  const DicePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Dados')),
      body: const DiceSheet(embedded: true),
    );
  }
}
