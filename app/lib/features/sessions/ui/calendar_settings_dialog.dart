import 'package:flutter/material.dart';

import '../domain/sessions_format.dart';

/// Result of [CalendarSettingsDialog].
typedef CalendarSettings = ({String timeZoneId, List<int> reminderOffsetsMinutes});

const _maxOffsets = 10;
const _maxOffsetMinutes = 43200;

/// Edits the time zone of a campaign (a list of common IANA zones, or any other
/// typed by hand) and the reminder offsets (editable chips, in hours or
/// minutes before the session). Pops a [CalendarSettings] or null.
class CalendarSettingsDialog extends StatefulWidget {
  const CalendarSettingsDialog({
    super.key,
    required this.initialTimeZoneId,
    required this.initialOffsets,
  });

  final String initialTimeZoneId;
  final List<int> initialOffsets;

  @override
  State<CalendarSettingsDialog> createState() => _CalendarSettingsDialogState();
}

enum _Unit {
  hours('horas', 60),
  minutes('minutos', 1);

  const _Unit(this.label, this.factor);

  final String label;
  final int factor;
}

class _CalendarSettingsDialogState extends State<CalendarSettingsDialog> {
  late String _zone;
  late List<int> _offsets;
  final _customZone = TextEditingController();
  final _amount = TextEditingController();
  _Unit _unit = _Unit.hours;
  String? _zoneError;
  String? _offsetError;

  List<String> get _zones => [
    if (!commonTimeZones.contains(widget.initialTimeZoneId)) widget.initialTimeZoneId,
    ...commonTimeZones,
  ];

  @override
  void initState() {
    super.initState();
    _zone = widget.initialTimeZoneId;
    _offsets = [...widget.initialOffsets]..sort((a, b) => b.compareTo(a));
  }

  @override
  void dispose() {
    _customZone.dispose();
    _amount.dispose();
    super.dispose();
  }

  void _addOffset() {
    final amount = int.tryParse(_amount.text.trim());
    if (amount == null || amount < 1) {
      setState(() => _offsetError = 'Escribe un número mayor que 0.');
      return;
    }
    final minutes = amount * _unit.factor;
    String? error;
    if (minutes > _maxOffsetMinutes) {
      error = 'Como máximo 30 días (720 horas).';
    } else if (_offsets.contains(minutes)) {
      error = 'Ese recordatorio ya existe.';
    } else if (_offsets.length >= _maxOffsets) {
      error = 'Como máximo $_maxOffsets recordatorios.';
    }
    setState(() {
      _offsetError = error;
      if (error == null) {
        _offsets = [..._offsets, minutes]..sort((a, b) => b.compareTo(a));
        _amount.clear();
      }
    });
  }

  void _save() {
    final custom = _customZone.text.trim();
    final zone = custom.isEmpty ? _zone : custom;
    if (!isKnownTimeZone(zone)) {
      setState(
        () => _zoneError = 'Zona no reconocida. Usa un identificador IANA, p. ej. Europe/Madrid.',
      );
      return;
    }
    Navigator.of(context).pop((timeZoneId: zone, reminderOffsetsMinutes: _offsets));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return AlertDialog(
      title: const Text('Ajustes del calendario'),
      content: SizedBox(
        width: double.maxFinite,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              DropdownButtonFormField<String>(
                key: const Key('settings-zone'),
                initialValue: _zone,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Zona horaria'),
                items: [
                  for (final z in _zones) DropdownMenuItem(value: z, child: Text(z)),
                ],
                onChanged: (value) => setState(() {
                  _zone = value ?? _zone;
                  _zoneError = null;
                }),
              ),
              TextField(
                key: const Key('settings-zone-custom'),
                controller: _customZone,
                decoration: InputDecoration(
                  labelText: 'Otra zona (opcional)',
                  hintText: 'Por ejemplo Asia/Tokyo',
                  errorText: _zoneError,
                ),
                onChanged: (_) => setState(() => _zoneError = null),
              ),
              const SizedBox(height: 20),
              Text('Recordatorios por correo', style: theme.textTheme.titleSmall),
              const SizedBox(height: 4),
              Text(
                'Se envían antes de cada sesión programada.',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 8),
              if (_offsets.isEmpty)
                const Text('Sin recordatorios.', key: Key('settings-no-offsets'))
              else
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    for (final minutes in _offsets)
                      InputChip(
                        key: Key('settings-offset-$minutes'),
                        label: Text(formatOffsetBefore(minutes)),
                        onDeleted: () => setState(() => _offsets = [..._offsets]..remove(minutes)),
                      ),
                  ],
                ),
              const SizedBox(height: 8),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 80,
                    child: TextField(
                      key: const Key('settings-offset-amount'),
                      controller: _amount,
                      keyboardType: TextInputType.number,
                      decoration: InputDecoration(labelText: 'Antes', errorText: _offsetError),
                      onSubmitted: (_) => _addOffset(),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: DropdownButton<_Unit>(
                        key: const Key('settings-offset-unit'),
                        value: _unit,
                        isExpanded: true,
                        items: [
                          for (final u in _Unit.values)
                            DropdownMenuItem(value: u, child: Text(u.label)),
                        ],
                        onChanged: (value) => setState(() => _unit = value ?? _unit),
                      ),
                    ),
                  ),
                  IconButton(
                    key: const Key('settings-offset-add'),
                    tooltip: 'Añadir recordatorio',
                    onPressed: _addOffset,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('settings-save'),
          onPressed: _save,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}
