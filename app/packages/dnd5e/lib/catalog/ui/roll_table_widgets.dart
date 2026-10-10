import 'package:flutter/material.dart';

import 'package:opentrpg_core/core/ui/source_chip.dart';
import 'package:opentrpg_core/features/dice/ui/roll_input_button.dart';

import '../data/models.dart';

/// Looks up a physical roll in [table]: the player types the result ("Tira
/// 1d100", Key `roll-table-input`) and the matching entry is shown (Key
/// `roll-table-result`). On a d100, "00" is 100. The "Tirar" button rolls
/// it with the virtual dice instead.
class RollTableLookup extends StatefulWidget {
  const RollTableLookup({super.key, required this.table, this.autofocus = false});

  final RollTable table;
  final bool autofocus;

  @override
  State<RollTableLookup> createState() => _RollTableLookupState();
}

class _RollTableLookupState extends State<RollTableLookup> {
  final _input = TextEditingController();

  @override
  void dispose() {
    _input.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final table = widget.table;
    final text = _input.text.trim();
    final roll = table.parseRoll(text);
    final entry = roll == null ? null : table.entryFor(roll);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        TextField(
          key: const Key('roll-table-input'),
          controller: _input,
          autofocus: widget.autofocus,
          keyboardType: TextInputType.number,
          maxLength: 3,
          decoration: InputDecoration(
            labelText: 'Tira 1${table.dice}',
            helperText: table.faces == 100 ? '00 cuenta como 100' : null,
            counterText: '',
            suffixIcon: RollInputButton(
              key: const Key('roll-table-dice'),
              expression: '1d${table.faces}',
              label: table.name,
              onRolled: (total, _) => setState(() => _input.text = '$total'),
            ),
          ),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: 8),
        if (text.isNotEmpty && roll == null)
          Text(
            'Escribe un resultado de 1 a ${table.faces}.',
            key: const Key('roll-table-error'),
            style: TextStyle(color: theme.colorScheme.error),
          )
        else if (roll != null)
          Card(
            key: const Key('roll-table-result'),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    entry == null ? 'Resultado $roll' : 'Resultado $roll (${entry.range})',
                    style: theme.textTheme.labelLarge,
                  ),
                  const SizedBox(height: 4),
                  SelectableText(entry?.text ?? 'La tabla no tiene entrada para este resultado.'),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

/// Bottom sheet with [RollTableLookup] for [table] (combat panel).
Future<void> showRollTableSheet(BuildContext context, RollTable table) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => Padding(
        padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + MediaQuery.viewInsetsOf(context).bottom),
        child: Column(
          key: const Key('roll-table-sheet'),
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(table.name, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            RollTableLookup(table: table, autofocus: true),
          ],
        ),
      ),
    );

/// Whole roll table with a search over its entries and the roll lookup on top
/// (compendium, section "Tablas").
class RollTablePage extends StatefulWidget {
  const RollTablePage({super.key, required this.table});

  final RollTable table;

  @override
  State<RollTablePage> createState() => _RollTablePageState();
}

class _RollTablePageState extends State<RollTablePage> {
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final table = widget.table;
    final search = _search.trim().toLowerCase();
    final entries = [
      for (final e in table.entries)
        if (search.isEmpty || e.text.toLowerCase().contains(search) || e.range == search) e,
    ];
    return Scaffold(
      appBar: AppBar(title: NameWithSource(table.name, table.source)),
      body: ListView(
        key: const Key('roll-table-page'),
        padding: const EdgeInsets.all(16),
        children: [
          RollTableLookup(table: table),
          const SizedBox(height: 16),
          TextField(
            key: const Key('roll-table-search'),
            decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              hintText: 'Buscar en la tabla',
            ),
            onChanged: (value) => setState(() => _search = value),
          ),
          const SizedBox(height: 8),
          if (entries.isEmpty) const Text('Ninguna entrada coincide.'),
          for (final e in entries)
            ListTile(
              key: Key('roll-table-entry-${e.from}'),
              contentPadding: EdgeInsets.zero,
              leading: SizedBox(
                width: 64,
                child: Text(e.range, style: Theme.of(context).textTheme.labelLarge),
              ),
              title: Text(e.text),
            ),
        ],
      ),
    );
  }
}
