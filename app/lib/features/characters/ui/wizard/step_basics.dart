import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/ui/source_chip.dart';
import '../../../campaigns/data/campaigns_controller.dart';
import '../../../campaigns/domain/campaign_models.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' hide Page;
import '../../../catalog/domain/catalog_format.dart';
import '../../../catalog/ui/catalog_detail_links.dart';
import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import '../../domain/class_theme.dart';
import '../height_weight_fields.dart';

const _npcValue = '__npc__';
const _noAlignment = '__none__';

/// Padding shared by every step.
const stepPadding = EdgeInsets.fromLTRB(16, 8, 16, 16);

String bonusesText(List<AbilityBonus> bonuses) => bonuses
    .map((b) => '${abilityLabel(b.ability)} ${b.bonus >= 0 ? '+' : ''}${b.bonus}')
    .join(', ');

/// Error text with a retry button for a catalog list that failed to load.
class WizardLoadError extends StatelessWidget {
  const WizardLoadError({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(describeApiError(error), textAlign: TextAlign.center),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('Reintentar'),
          ),
        ],
      ),
    ),
  );
}

// ---------------------------------------------------------------------------
// 1. Name
// ---------------------------------------------------------------------------

/// Name (required), optional alignment and, for DMs, which player it is for
/// (an NPC by default: a DM has no characters of their own).
class NameStep extends ConsumerStatefulWidget {
  const NameStep({super.key, required this.args});

  final WizardArgs args;

  @override
  ConsumerState<NameStep> createState() => _NameStepState();
}

class _NameStepState extends ConsumerState<NameStep> {
  late final _name = TextEditingController(
    text: ref.read(characterWizardControllerProvider(widget.args)).name,
  );

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  /// A DM has no characters of their own: anything but a player is an NPC.
  String _ownerValue(({String? userId})? owner, List<Member> players) {
    final id = owner?.userId;
    return players.any((m) => m.userId == id) ? id! : _npcValue;
  }

  @override
  Widget build(BuildContext context) {
    final args = widget.args;
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final campaign = ref.watch(campaignDetailControllerProvider(args.campaignId)).value;
    final canChooseOwner = campaign?.myRole.isAtLeastDm ?? false;
    final players = [
      for (final m in campaign?.members ?? const <Member>[])
        if (m.role == CampaignRole.player) m,
    ];

    return ListView(
      key: const Key('step-name'),
      padding: stepPadding,
      children: [
        TextField(
          key: const Key('wizard-name'),
          controller: _name,
          maxLength: 100,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Nombre del personaje'),
          onChanged: controller.setName,
        ),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: const Key('wizard-alignment'),
          initialValue: state.alignment ?? _noAlignment,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Alineamiento (opcional)'),
          items: [
            const DropdownMenuItem(value: _noAlignment, child: Text('Sin definir')),
            for (final e in alignments.entries)
              DropdownMenuItem(value: e.key, child: Text(e.value)),
          ],
          onChanged: (value) => controller.setAlignment(value == _noAlignment ? null : value),
        ),
        if (canChooseOwner) ...[
          const SizedBox(height: 16),
          DropdownButtonFormField<String>(
            key: const Key('wizard-owner'),
            initialValue: _ownerValue(state.owner, players),
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Para'),
            items: [
              const DropdownMenuItem(value: _npcValue, child: Text('PNJ (sin jugador)')),
              for (final m in players)
                DropdownMenuItem(
                  value: m.userId,
                  child: Text(m.displayName, overflow: TextOverflow.ellipsis),
                ),
            ],
            onChanged: (value) => controller.setOwner(
              value == null || value == _npcValue ? (userId: null) : (userId: value),
            ),
          ),
        ],
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// 2. Race
// ---------------------------------------------------------------------------

/// Race cards, then the subrace and the "apply racial bonuses" switch.
class RaceStep extends ConsumerWidget {
  const RaceStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final races = ref.watch(racesProvider);

    return races.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) =>
          WizardLoadError(error: error, onRetry: () => ref.invalidate(racesProvider)),
      data: (list) => ListView(
        key: const Key('step-race'),
        padding: stepPadding,
        children: [
          for (final race in list)
            WizardChoiceCard(
              key: Key('race-${race.index}'),
              source: race.source,
              selected: race.index == state.raceIndex,
              leading: const AppIcon(AppIcons.hood, size: 28),
              title: race.name,
              lines: [
                if (race.speed != null) 'Velocidad ${race.speed} pies',
                if (race.abilityBonuses.isNotEmpty) bonusesText(race.abilityBonuses),
              ],
              onTap: () => controller.selectRace(race.index),
              infoKey: Key('detail-race-${race.index}'),
              onInfo: () => openRaceDetail(context, race.index),
            ),
          if (state.raceIndex != null) ...[
            const SizedBox(height: 8),
            if (state.race == null && state.loadError == null)
              const Center(child: CircularProgressIndicator())
            else if (state.loadError != null && state.race == null)
              _InlineError(
                message: state.loadError!,
                onRetry: () => controller.selectRace(state.raceIndex!),
              )
            else ...[
              if (state.race!.subraces.isNotEmpty) ...[
                Text('Subraza', style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 4),
                for (final sub in state.race!.subraces)
                  WizardChoiceCard(
                    key: Key('subrace-${sub.index}'),
                    selected: sub.index == state.subraceIndex,
                    title: sub.name,
                    lines: [if (sub.abilityBonuses.isNotEmpty) bonusesText(sub.abilityBonuses)],
                    onTap: () => controller.selectSubrace(sub.index),
                  ),
              ],
              SwitchListTile(
                key: const Key('wizard-racial-bonuses'),
                contentPadding: EdgeInsets.zero,
                title: const Text('Aplicar bonos raciales'),
                subtitle: const Text('Se suman a las puntuaciones base.'),
                value: state.applyRacialBonuses,
                onChanged: controller.setApplyRacialBonuses,
              ),
              const SizedBox(height: 8),
              HeightWeightFields(
                key: const Key('basics-height-weight'),
                keyPrefix: 'basics',
                initialHeight: state.heightText,
                initialWeight: state.weightText,
                table: state.heightWeightTable,
                onHeightChanged: controller.setHeightText,
                onWeightChanged: controller.setWeightText,
              ),
            ],
          ],
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// 3. Class
// ---------------------------------------------------------------------------

/// Class cards with their accent and icon; starts at level 1.
class ClassStep extends ConsumerWidget {
  const ClassStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final classes = ref.watch(classesProvider);

    return classes.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) =>
          WizardLoadError(error: error, onRetry: () => ref.invalidate(classesProvider)),
      data: (list) => ListView(
        key: const Key('step-class'),
        padding: stepPadding,
        children: [
          Text('Nivel inicial: 1', style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 4),
          for (final c in list)
            WizardChoiceCard(
              key: Key('class-${c.index}'),
              selected: c.index == state.classIndex,
              accent: classAccentOf(context, c.index),
              leading: AppIcon(
                classThemeOf(c.index).icon,
                size: 28,
                color: classAccentOf(context, c.index),
              ),
              title: classThemeOf(c.index).labelEs == adventurerTheme.labelEs
                  ? c.name
                  : classThemeOf(c.index).labelEs,
              lines: [
                if (c.hitDie != null) 'Dado de golpe d${c.hitDie}',
                if (c.isSpellcaster)
                  'Lanza conjuros'
                      '${c.spellcastingAbility == null ? '' : ' (${abilityLabel(c.spellcastingAbility!)})'}'
                else
                  'No lanza conjuros',
              ],
              onTap: () => controller.selectClass(c.index),
              infoKey: Key('detail-class-${c.index}'),
              onInfo: () => openClassDetail(context, c.index),
            ),
          if (state.classIndex != null) ...[
            const SizedBox(height: 8),
            if (state.classDetail == null && state.loadError == null)
              const Center(child: CircularProgressIndicator())
            else if (state.classDetail == null)
              _InlineError(
                message: state.loadError!,
                onRetry: () => controller.selectClass(state.classIndex!),
              )
            else
              _ClassSummary(state: state, controller: controller),
          ],
        ],
      ),
    );
  }
}

class _ClassSummary extends StatelessWidget {
  const _ClassSummary({required this.state, required this.controller});

  final WizardState state;
  final CharacterWizardController controller;

  @override
  Widget build(BuildContext context) {
    final detail = state.classDetail!;
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (detail.savingThrows.isNotEmpty)
          Text('Salvaciones: ${detail.savingThrows.map(abilityLabel).join(', ')}'),
        if (detail.skillChoices.choose > 0)
          Text('Habilidades a elegir: ${detail.skillChoices.choose}'),
        if (state.needsSubclass) ...[
          const SizedBox(height: 12),
          Text(
            detail.subclassFlavor == null || detail.subclassFlavor!.isEmpty
                ? 'Subclase'
                : detail.subclassFlavor!,
            style: theme.textTheme.titleSmall,
          ),
          const SizedBox(height: 4),
          for (final sub in detail.subclasses)
            WizardChoiceCard(
              key: Key('subclass-${sub.index}'),
              source: sub.source,
              selected: sub.index == state.subclassIndex,
              title: sub.name,
              lines: [if (sub.flavor != null) sub.flavor!],
              onTap: () => controller.selectSubclass(sub.index),
              infoKey: Key('detail-subclass-${sub.index}'),
              onInfo: () => openClassDetail(context, detail.index),
            ),
        ],
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Shared pieces
// ---------------------------------------------------------------------------

class _InlineError extends StatelessWidget {
  const _InlineError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      Expanded(
        child: Text(message, style: TextStyle(color: Theme.of(context).colorScheme.error)),
      ),
      TextButton(onPressed: onRetry, child: const Text('Reintentar')),
    ],
  );
}

/// Selectable card: an optional [leading] icon, a title and detail lines.
class WizardChoiceCard extends StatelessWidget {
  const WizardChoiceCard({
    super.key,
    required this.selected,
    required this.title,
    required this.onTap,
    this.lines = const [],
    this.leading,
    this.accent,
    this.source,
    this.onInfo,
    this.infoKey,
  });

  final bool selected;

  /// Opens the catalog detail of the choice; when set, an info button is
  /// shown next to the selection mark (tapping it does not select).
  final VoidCallback? onInfo;
  final Key? infoKey;
  final String title;

  /// Origin of the content ('srd', 'homebrew' or a content pack id); shown as a chip.
  final String? source;
  final List<String> lines;
  final Widget? leading;
  final Color? accent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final color = accent ?? theme.colorScheme.primary;
    return Card(
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: selected ? color : theme.colorScheme.outlineVariant,
          width: selected ? 2 : 1,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              if (leading != null) ...[leading!, const SizedBox(width: 12)],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // The info button and the selection mark stay on the
                    // title's line, centred with it.
                    Row(
                      children: [
                        Expanded(
                          child: NameWithSource(title, source, style: theme.textTheme.titleMedium),
                        ),
                        if (onInfo != null) DetailInfoButton(key: infoKey, onPressed: onInfo!),
                        if (selected) Icon(Icons.check_circle, color: color),
                      ],
                    ),
                    for (final line in lines.where((l) => l.isNotEmpty))
                      Text(line, style: theme.textTheme.bodySmall),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
