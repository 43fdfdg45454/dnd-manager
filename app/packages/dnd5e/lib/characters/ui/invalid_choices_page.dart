import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';
import 'package:opentrpg_core/core/ui/offline_widgets.dart';
import 'package:opentrpg_core/features/characters/data/characters_controller.dart';

import '../dnd5e_characters_controller.dart';
import '../domain/character_format.dart' show abilityAbbreviation;
import '../models.dart';
import 'level_up/level_up_widgets.dart';

/// "Sustituye lo que ya no cumples" (`/characters/:id/invalid-choices`): the
/// options and feats whose prerequisites no longer hold must be replaced by
/// another one. One card list per invalid pick; the player cannot leave the
/// page while there are invalid picks.
class InvalidChoicesPage extends ConsumerStatefulWidget {
  const InvalidChoicesPage({super.key, required this.characterId});

  final String characterId;

  @override
  ConsumerState<InvalidChoicesPage> createState() => _InvalidChoicesPageState();
}

class _InvalidChoicesPageState extends ConsumerState<InvalidChoicesPage> {
  /// Picked option (or feat) by replacement key.
  final Map<String, String> _picked = {};

  /// Ability chosen for a feat by replacement key.
  final Map<String, String> _abilities = {};
  bool _busy = false;
  String? _error;

  Dnd5eCharacterController get _controller =>
      ref.read(dnd5eCharacterControllerProvider(widget.characterId).notifier);

  bool _answered(LevelUpChoice choice) {
    if (choice.required == 0) return true;
    final index = _picked[choice.key];
    if (index == null) return false;
    final increase = choice.option(index)?.abilityIncrease;
    if (choice.kind == LevelChoiceKind.asiOrFeat && increase != null && increase.needsPick) {
      return _abilities[choice.key] != null;
    }
    return true;
  }

  List<LevelUpChoiceAnswer> _answers(InvalidChoices plan) {
    final answers = <LevelUpChoiceAnswer>[];
    for (final choice in plan.choices) {
      final index = _picked[choice.key];
      if (index == null) {
        answers.add(LevelUpChoiceAnswer.picks(choice.key, const []));
      } else if (choice.kind == LevelChoiceKind.asiOrFeat) {
        final increase = choice.option(index)?.abilityIncrease;
        answers.add(
          LevelUpChoiceAnswer.feat(
            choice.key,
            index,
            ability: increase == null
                ? null
                : _abilities[choice.key] ?? (increase.needsPick ? null : increase.options.first),
          ),
        );
      } else {
        answers.add(LevelUpChoiceAnswer.picks(choice.key, [index]));
      }
    }
    return answers;
  }

  Future<void> _confirm(InvalidChoices plan) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await _controller.replaceInvalidChoices(_answers(plan));
      if (!mounted) return;
      ref.invalidate(invalidChoicesProvider(widget.characterId));
      // The character no longer has invalid picks, so the page may pop.
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
    final planAsync = ref.watch(invalidChoicesProvider(widget.characterId));
    final pending = detail.value?.invalidChoices.isNotEmpty ?? false;

    return PopScope(
      canPop: !pending || (!_busy && planAsync.hasError),
      child: Scaffold(
        key: const Key('invalid-choices'),
        appBar: AppBar(
          title: const Text('Sustituye lo que ya no cumples'),
          automaticallyImplyLeading: !pending,
        ),
        body: planAsync.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(describeCharacterError(error), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: () => ref.invalidate(invalidChoicesProvider(widget.characterId)),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ),
          data: _body,
        ),
      ),
    );
  }

  Widget _body(InvalidChoices plan) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final complete = plan.choices.every(_answered);
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
            children: [
              Text(
                'Tus características han cambiado y ya no cumples los requisitos de lo siguiente. '
                'Elige otra opción para cada uno.',
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: 12),
              for (final choice in plan.choices) _replacement(plan, choice),
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
                key: const Key('invalid-choices-error'),
                style: TextStyle(color: tokens.blood),
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
                  key: const Key('invalid-choices-confirm'),
                  onPressed: canWrite && complete && !_busy ? () => _confirm(plan) : null,
                  icon: _busy
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.check),
                  label: const Text('Sustituir'),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _replacement(InvalidChoices plan, LevelUpChoice choice) {
    final theme = Theme.of(context);
    final invalid = plan.invalid.where((i) => i.replaceKey == choice.key).firstOrNull;
    final picked = _picked[choice.key];
    final pickedOption = picked == null ? null : choice.option(picked);
    final increase = pickedOption?.abilityIncrease;
    return Padding(
      key: Key('replace-${choice.key}'),
      padding: const EdgeInsets.only(bottom: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          LevelUpHeading(choice.name, subtitle: invalid?.reason ?? choice.note),
          if (choice.options.every((o) => !o.eligible) || choice.required == 0)
            Text(
              'No hay ninguna opción válida: se quitará sin sustituirla.',
              key: Key('replace-none-${choice.key}'),
              style: theme.textTheme.bodySmall,
            ),
          for (final option in choice.options)
            LevelUpOptionCard(
              key: Key('replace-option-${choice.key}-${option.index}'),
              option: option,
              selected: picked == option.index,
              onTap: () => setState(() {
                if (picked == option.index) {
                  _picked.remove(choice.key);
                } else {
                  _picked[choice.key] = option.index;
                }
                _abilities.remove(choice.key);
                _error = null;
              }),
            ),
          if (pickedOption != null &&
              choice.kind == LevelChoiceKind.asiOrFeat &&
              increase != null &&
              increase.needsPick) ...[
            const SizedBox(height: 8),
            Text(
              '${pickedOption.name} sube +${increase.amount} a:',
              style: theme.textTheme.titleSmall,
            ),
            Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final ability in increase.options)
                  ChoiceChip(
                    key: Key('replace-ability-${choice.key}-$ability'),
                    label: Text(abilityAbbreviation(ability)),
                    selected: _abilities[choice.key] == ability,
                    onSelected: (_) => setState(() => _abilities[choice.key] = ability),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
