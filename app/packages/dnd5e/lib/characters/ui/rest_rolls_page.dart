import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/core/ui/offline_widgets.dart';
import 'package:opentrpg_core/features/characters/data/characters_controller.dart';
import 'package:opentrpg_core/features/dice/ui/roll_input_button.dart';

import '../dnd5e_characters_controller.dart';
import '../models.dart';
import 'combat/combat_support.dart' show CombatCard;
import 'combat/resources_section.dart' show resourcesOf;
import 'level_up/level_up_widgets.dart' show LevelUpHeading;

/// Resources the player still has to roll for after a rest.
List<CharacterResource> pendingRollResources(CharacterDetail character) => [
  for (final r in resourcesOf(character))
    if (r.rollsPending && r.rollOnRest != null) r,
];

/// "Tira tus dados" (`/characters/:id/rest-rolls`): one field per die of each
/// resource that asks for a roll after the rest (Portent: two d20 after a long
/// rest), each 1..die, typed or rolled with the virtual dice. The player
/// cannot leave it while a roll is pending.
class RestRollsPage extends ConsumerStatefulWidget {
  const RestRollsPage({super.key, required this.characterId});

  final String characterId;

  @override
  ConsumerState<RestRollsPage> createState() => _RestRollsPageState();
}

class _RestRollsPageState extends ConsumerState<RestRollsPage> {
  /// Typed texts by `<resourceId>-<index>`.
  final Map<String, String> _texts = {};

  /// Field controllers by the same key, so a virtual roll can fill them.
  final Map<String, TextEditingController> _fields = {};
  bool _busy = false;

  TextEditingController _field(String key) =>
      _fields.putIfAbsent(key, () => TextEditingController(text: _texts[key] ?? ''));

  @override
  void dispose() {
    for (final f in _fields.values) {
      f.dispose();
    }
    super.dispose();
  }

  String? _error;

  Dnd5eCharacterController get _controller =>
      ref.read(dnd5eCharacterControllerProvider(widget.characterId).notifier);

  /// The value typed, or null when it is not a whole number in 1..[sides].
  int? _value(String resourceId, int index, int sides) {
    final value = int.tryParse((_texts['$resourceId-$index'] ?? '').trim());
    return value != null && value >= 1 && value <= sides ? value : null;
  }

  bool _complete(List<CharacterResource> pending) => pending.every(
    (r) => List.generate(
      r.rollOnRest!.count,
      (i) => _value(r.id, i, r.rollOnRest!.sides),
    ).every((v) => v != null),
  );

  Future<void> _save(List<CharacterResource> pending) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      for (final r in pending) {
        final roll = r.rollOnRest!;
        await _controller.saveResourceRolls(r.id, [
          for (var i = 0; i < roll.count; i++) _value(r.id, i, roll.sides)!,
        ]);
      }
      if (!mounted) return;
      // The character no longer has pending rolls, so the page may pop.
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && context.canPop()) context.pop();
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = problemDetail(error) ?? describeCharacterError(error);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(characterControllerProvider(widget.characterId));
    final character = detail.value;
    final pending = character == null
        ? const <CharacterResource>[]
        : pendingRollResources(character);
    final locked = character != null && (character.restRollsPending || pending.isNotEmpty);
    final theme = Theme.of(context);

    return PopScope(
      canPop: !locked,
      child: Scaffold(
        key: const Key('rest-rolls'),
        appBar: AppBar(title: const Text('Tira tus dados'), automaticallyImplyLeading: !locked),
        body: character == null
            ? const Center(child: CircularProgressIndicator())
            : Column(
                children: [
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                      children: [
                        const LevelUpHeading(
                          'Tiradas del descanso',
                          subtitle: 'Tira los dados en la mesa y escribe cada resultado, o usa el dado virtual.',
                        ),
                        if (pending.isEmpty)
                          Text('No hay tiradas pendientes.', style: theme.textTheme.bodyMedium),
                        for (final r in pending)
                          CombatCard(
                            key: Key('rest-roll-${r.id}'),
                            title: r.name,
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  r.rollOnRest!.count == 1
                                      ? 'Tira 1 ${r.rollOnRest!.dice}.'
                                      : 'Tira ${r.rollOnRest!.count} ${r.rollOnRest!.dice}.',
                                  style: theme.textTheme.bodySmall,
                                ),
                                for (var i = 0; i < r.rollOnRest!.count; i++)
                                  Padding(
                                    padding: const EdgeInsets.only(top: 8),
                                    child: TextField(
                                      key: Key('rest-roll-${r.id}-$i'),
                                      controller: _field('${r.id}-$i'),
                                      keyboardType: TextInputType.number,
                                      decoration: InputDecoration(
                                        labelText: 'Tirada ${i + 1} (1 a ${r.rollOnRest!.sides})',
                                        border: const OutlineInputBorder(),
                                        isDense: true,
                                        errorText:
                                            (_texts['${r.id}-$i'] ?? '').trim().isNotEmpty &&
                                                _value(r.id, i, r.rollOnRest!.sides) == null
                                            ? 'Escribe un número de 1 a ${r.rollOnRest!.sides}'
                                            : null,
                                        suffixIcon: RollInputButton(
                                          key: Key('rest-roll-${r.id}-$i-dice'),
                                          expression: '1d${r.rollOnRest!.sides}',
                                          label: '${r.name}: tirada ${i + 1}',
                                          onRolled: (total, _) => setState(() {
                                            _texts['${r.id}-$i'] = '$total';
                                            _field('${r.id}-$i').text = '$total';
                                          }),
                                        ),
                                      ),
                                      onChanged: (text) =>
                                          setState(() => _texts['${r.id}-$i'] = text),
                                    ),
                                  ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                      child: Align(
                        alignment: Alignment.centerLeft,
                        child: Text(
                          _error!,
                          key: const Key('rest-rolls-error'),
                          style: TextStyle(color: theme.colorScheme.error),
                        ),
                      ),
                    ),
                  SafeArea(
                    top: false,
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: SizedBox(
                        width: double.infinity,
                        child: OfflineAware(
                          builder: (context, canWrite) => FilledButton.icon(
                            key: const Key('rest-rolls-save'),
                            onPressed:
                                canWrite && pending.isNotEmpty && _complete(pending) && !_busy
                                ? () => _save(pending)
                                : null,
                            icon: _busy
                                ? const SizedBox.square(
                                    dimension: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Icon(Icons.check),
                            label: const Text('Guardar tiradas'),
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}
