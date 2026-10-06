import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/motion/level_up_celebration.dart';
import '../../../../core/motion/motion_settings.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../data/level_up_controller.dart';
import '../../data/models.dart';
import '../../domain/class_theme.dart';
import 'choice_step.dart';
import 'class_step.dart';
import 'hit_points_step.dart';
import 'review_step.dart';

/// Icon of a page of the level-up wizard.
AppIcons levelUpStepIcon(LevelUpState state, LevelUpStep step) => switch (step.kind) {
  LevelUpStepKind.classChoice => classThemeOf(state.selectedClassIndex).icon,
  LevelUpStepKind.hitPoints => AppIcons.heart,
  LevelUpStepKind.review => AppIcons.seal,
  LevelUpStepKind.choice => switch (state.choiceOf(step.choiceKey!)?.kind) {
    LevelChoiceKind.subclass => AppIcons.crown,
    LevelChoiceKind.asiOrFeat => AppIcons.levelUp,
    LevelChoiceKind.cantripsKnown ||
    LevelChoiceKind.spellsKnown ||
    LevelChoiceKind.spellbookSpells => AppIcons.spellbook,
    LevelChoiceKind.expertise => AppIcons.sparkles,
    LevelChoiceKind.skill => AppIcons.book,
    LevelChoiceKind.language => AppIcons.scroll,
    LevelChoiceKind.tool => AppIcons.anvil,
    _ => AppIcons.rune,
  },
};

/// Title of a page of the level-up wizard.
String levelUpStepTitle(LevelUpState state, LevelUpStep step) => switch (step.kind) {
  LevelUpStepKind.classChoice => 'Clase',
  LevelUpStepKind.hitPoints => 'Puntos de golpe',
  LevelUpStepKind.review => 'Resumen',
  LevelUpStepKind.choice => state.choiceOf(step.choiceKey!)?.name ?? 'Elección',
};

/// Full-screen level-up wizard (`/characters/:id/level-up`): class and
/// automatic features, the hit die roll, one page per choice and a review
/// with "Confirmar". On success it celebrates and goes back to "Mi sesión".
class LevelUpPage extends ConsumerStatefulWidget {
  const LevelUpPage({super.key, required this.characterId});

  final String characterId;

  @override
  ConsumerState<LevelUpPage> createState() => _LevelUpPageState();
}

class _LevelUpPageState extends ConsumerState<LevelUpPage> {
  final _pages = PageController();

  /// Page whose error is shown (after a failed "Siguiente").
  int? _attemptedStep;

  LevelUpController get _controller =>
      ref.read(levelUpControllerProvider(widget.characterId).notifier);

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _next() {
    final step = ref.read(levelUpControllerProvider(widget.characterId)).step;
    final error = _controller.next();
    setState(() => _attemptedStep = error == null ? null : step);
  }

  Future<void> _confirm() async {
    final state = ref.read(levelUpControllerProvider(widget.characterId));
    final level = state.plan?.targetLevel ?? 0;
    final campaignId = state.character?.campaignId;
    final ok = await _controller.submit();
    if (!ok || !mounted) return;
    await showLevelUpCelebration(context, level: level);
    if (!mounted) return;
    if (context.canPop()) {
      context.pop();
    } else {
      final target = ref.read(levelUpControllerProvider(widget.characterId)).completed;
      final campaign = target?.campaignId ?? campaignId;
      context.go(
        campaign == null
            ? AppRoutes.character(widget.characterId)
            : AppRoutes.campaignPlayerView(campaign),
      );
    }
  }

  void _syncPage(int step) {
    if (!_pages.hasClients) return;
    if (MotionScope.reducedOf(context)) {
      _pages.jumpToPage(step);
    } else {
      _pages.animateToPage(
        step,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOut,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = levelUpControllerProvider(widget.characterId);
    final state = ref.watch(provider);
    ref.listen(provider.select((s) => s.step), (_, step) => _syncPage(step));
    final steps = state.steps;
    final step = state.step.clamp(0, steps.length - 1);
    final current = steps[step];
    final isLast = current.kind == LevelUpStepKind.review;
    final error = _attemptedStep == step ? state.validate(step) : null;
    final scheme = Theme.of(context).colorScheme;
    final name = state.character?.name;

    // A granted level is mandatory: the page can only be left once it is applied
    // (or when it cannot even load, so the player is not trapped).
    final locked = state.completed == null && state.loadError == null;
    return PopScope(
      canPop: !locked,
      child: Scaffold(
        key: const Key('levelup-page'),
        appBar: AppBar(
          automaticallyImplyLeading: !locked,
          title: Text(name == null ? 'Subir de nivel' : 'Subir de nivel · $name'),
        ),
        body: Column(
          children: [
            _ProgressRail(
              state: state,
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
                  'Paso ${step + 1} de ${steps.length} · ${levelUpStepTitle(state, current)}',
                  key: const Key('levelup-step-title'),
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _pages,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: steps.length,
                itemBuilder: (context, i) =>
                    KeyedSubtree(key: Key('levelup-${steps[i].id}'), child: _buildStep(steps[i])),
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
                    key: const Key('levelup-error'),
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
                      key: const Key('levelup-back'),
                      onPressed: step == 0 || state.submitting ? null : _controller.back,
                      icon: const Icon(Icons.arrow_back),
                      label: const Text('Atrás'),
                    ),
                    const Spacer(),
                    if (isLast)
                      OfflineAware(
                        builder: (context, canWrite) => FilledButton.icon(
                          key: const Key('levelup-confirm'),
                          onPressed: canWrite && state.canConfirm ? _confirm : null,
                          icon: state.submitting
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const AppIcon(AppIcons.levelUp, size: 18),
                          label: const Text('Confirmar'),
                        ),
                      )
                    else
                      FilledButton.icon(
                        key: const Key('levelup-next'),
                        onPressed: _next,
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

  Widget _buildStep(LevelUpStep step) => switch (step.kind) {
    LevelUpStepKind.classChoice => LevelUpClassStep(characterId: widget.characterId),
    LevelUpStepKind.hitPoints => LevelUpHitPointsStep(characterId: widget.characterId),
    LevelUpStepKind.choice => LevelUpChoiceStep(
      characterId: widget.characterId,
      choiceKey: step.choiceKey!,
    ),
    LevelUpStepKind.review => LevelUpReviewStep(characterId: widget.characterId),
  };
}

/// One icon per page; the current one in ember, completed ones in old gold.
/// Earlier pages can be tapped to go back.
class _ProgressRail extends StatelessWidget {
  const _ProgressRail({
    required this.state,
    required this.steps,
    required this.current,
    required this.onTap,
  });

  final LevelUpState state;
  final List<LevelUpStep> steps;
  final int current;
  final ValueChanged<int> onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
      child: Row(
        children: [
          for (var i = 0; i < steps.length; i++) ...[
            if (i > 0)
              Container(width: 16, height: 2, color: i <= current ? tokens.oldGold : tokens.rune),
            InkResponse(
              key: Key('levelup-dot-${steps[i].id}'),
              onTap: () => onTap(i),
              radius: 22,
              child: Semantics(
                label: levelUpStepTitle(state, steps[i]),
                selected: i == current,
                child: CircleAvatar(
                  radius: 16,
                  backgroundColor: i == current
                      ? tokens.ember
                      : i < current
                      ? tokens.oldGold.withValues(alpha: 0.35)
                      : tokens.stoneRaised,
                  child: AppIcon(
                    levelUpStepIcon(state, steps[i]),
                    size: 18,
                    color: i == current ? tokens.obsidian : tokens.bone,
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
