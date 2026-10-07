import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../data/character_wizard_controller.dart';
import 'step_abilities.dart';
import 'step_basics.dart';
import 'step_equipment.dart';
import 'step_origin.dart';
import 'step_proficiencies.dart';
import 'step_review.dart';
import 'step_spells.dart';

/// Title and icon of each wizard step.
extension WizardStepInfo on WizardStep {
  String get title => switch (this) {
    WizardStep.name => 'Nombre',
    WizardStep.race => 'Raza',
    WizardStep.classChoice => 'Clase',
    WizardStep.abilities => 'Características',
    WizardStep.background => 'Trasfondo',
    WizardStep.origin => 'Elecciones de raza y trasfondo',
    WizardStep.equipment => 'Equipo',
    WizardStep.spells => 'Hechizos',
    WizardStep.review => 'Revisión',
  };

  AppIcons get icon => switch (this) {
    WizardStep.name => AppIcons.quill,
    WizardStep.race => AppIcons.hood,
    WizardStep.classChoice => AppIcons.sword,
    WizardStep.abilities => AppIcons.d20,
    WizardStep.background => AppIcons.book,
    WizardStep.origin => AppIcons.hood,
    WizardStep.equipment => AppIcons.backpack,
    WizardStep.spells => AppIcons.spellbook,
    WizardStep.review => AppIcons.scroll,
  };
}

/// Step-by-step creation of a character: name, race, class, abilities,
/// background, equipment, spells and a final review. [ownerUserId] (DMs only)
/// preselects the owner.
class CharacterWizardPage extends ConsumerStatefulWidget {
  const CharacterWizardPage({super.key, required this.campaignId, this.ownerUserId});

  final String campaignId;
  final String? ownerUserId;

  @override
  ConsumerState<CharacterWizardPage> createState() => _CharacterWizardPageState();
}

class _CharacterWizardPageState extends ConsumerState<CharacterWizardPage> {
  final _pages = PageController();
  int? _attemptedStep;
  bool _submitting = false;
  String? _submitError;

  WizardArgs get _args => (campaignId: widget.campaignId, ownerUserId: widget.ownerUserId);

  CharacterWizardController get _controller =>
      ref.read(characterWizardControllerProvider(_args).notifier);

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  bool _advancing = false;

  Future<void> _next() async {
    if (_advancing) return;
    final before = ref.read(characterWizardControllerProvider(_args));
    final step = before.step;
    final validation = before.validate(step);
    setState(() => _advancing = true);
    final error = await _controller.advance();
    if (!mounted) return;
    setState(() {
      _advancing = false;
      _attemptedStep = error == null ? null : step;
      // Only a failed save is kept; validation errors are recomputed live.
      _advanceError = error != null && error != validation ? error : null;
    });
  }

  /// Error of the last "Siguiente" that the state cannot recompute (a failed
  /// save of the origin choices).
  String? _advanceError;

  Future<void> _submit() async {
    final step = ref.read(characterWizardControllerProvider(_args)).step;
    final error = _controller.validate(step);
    if (error != null) {
      setState(() => _attemptedStep = step);
      return;
    }
    setState(() {
      _submitting = true;
      _submitError = null;
    });
    try {
      final id = await _controller.submit();
      if (!mounted) return;
      context.pushReplacement(AppRoutes.character(id));
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _submitError = describeCharacterError(error);
      });
    }
  }

  Future<bool> _confirmDiscard() async {
    final discard = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Descartar el personaje?'),
        content: const Text('Se perderán los datos que has introducido.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Seguir editando'),
          ),
          FilledButton(
            key: const Key('wizard-discard'),
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Descartar'),
          ),
        ],
      ),
    );
    return discard ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(characterWizardControllerProvider(_args));
    ref.listen(characterWizardControllerProvider(_args).select((s) => s.step), (_, step) {
      if (_pages.hasClients) {
        _pages.animateToPage(
          step,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
    final steps = state.steps;
    final step = state.step.clamp(0, steps.length - 1);
    final current = steps[step];
    final isLast = step == steps.length - 1;
    final error = _attemptedStep == step ? (_advanceError ?? state.validate(step)) : null;
    final scheme = Theme.of(context).colorScheme;

    return PopScope(
      canPop: !_controller.isDirty || _submitting,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        final navigator = Navigator.of(context);
        if (await _confirmDiscard()) {
          await _controller.discardDraft();
          navigator.pop();
        }
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('Nuevo personaje')),
        body: Column(
          children: [
            _ProgressRail(
              steps: steps,
              current: step,
              onTap: (i) {
                if (i < step) _controller.goTo(i);
              },
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Paso ${step + 1} de ${steps.length} · ${current.title}',
                  key: const Key('wizard-step-title'),
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _pages,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: steps.length,
                itemBuilder: (context, i) => _buildStep(steps[i]),
              ),
            ),
          ],
        ),
        bottomNavigationBar: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (error != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    error,
                    key: const Key('wizard-error'),
                    style: TextStyle(color: scheme.error),
                  ),
                ),
              ),
            if (_submitError != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _submitError!,
                    key: const Key('wizard-submit-error'),
                    style: TextStyle(color: scheme.error),
                  ),
                ),
              ),
            SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    OutlinedButton.icon(
                      key: const Key('wizard-back'),
                      onPressed: step == 0 || _submitting ? null : _controller.back,
                      icon: const Icon(Icons.arrow_back),
                      label: const Text('Atrás'),
                    ),
                    const Spacer(),
                    if (isLast)
                      OfflineAware(
                        builder: (context, canWrite) => FilledButton.icon(
                          key: const Key('wizard-submit'),
                          onPressed: canWrite && !_submitting ? _submit : null,
                          icon: _submitting
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.check),
                          label: const Text('Crear personaje'),
                        ),
                      )
                    else
                      FilledButton.icon(
                        key: const Key('wizard-next'),
                        onPressed: _advancing ? null : _next,
                        icon: const Icon(Icons.arrow_forward),
                        label: const Text('Siguiente'),
                      ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStep(WizardStep step) => switch (step) {
    WizardStep.name => NameStep(args: _args),
    WizardStep.race => RaceStep(args: _args),
    WizardStep.classChoice => ClassStep(args: _args),
    WizardStep.abilities => AbilitiesStep(args: _args),
    WizardStep.background => BackgroundStep(args: _args),
    WizardStep.origin => OriginStep(args: _args),
    WizardStep.equipment => EquipmentStep(args: _args),
    WizardStep.spells => SpellsStep(args: _args),
    WizardStep.review => ReviewStep(args: _args),
  };
}

/// Row of dots (one icon per step); completed and current ones are highlighted
/// and earlier ones can be tapped to go back.
class _ProgressRail extends StatelessWidget {
  const _ProgressRail({required this.steps, required this.current, required this.onTap});

  final List<WizardStep> steps;
  final int current;
  final ValueChanged<int> onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          for (var i = 0; i < steps.length; i++)
            InkResponse(
              key: Key('wizard-dot-${steps[i].name}'),
              onTap: () => onTap(i),
              radius: 22,
              child: Semantics(
                label: steps[i].title,
                selected: i == current,
                child: CircleAvatar(
                  radius: 16,
                  backgroundColor: i == current
                      ? scheme.primary
                      : i < current
                      ? scheme.primaryContainer
                      : scheme.surfaceContainerHighest,
                  child: AppIcon(
                    steps[i].icon,
                    size: 18,
                    color: i == current ? scheme.onPrimary : scheme.onSurfaceVariant,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
