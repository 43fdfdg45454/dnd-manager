import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/stat_value.dart';
import '../../../catalog/data/beast_models.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/domain/catalog_format.dart' show abilityLabel;
import '../../../catalog/ui/beast_page.dart' show BeastRollChips;
import '../../data/characters_controller.dart';
import '../../data/models.dart';
import '../../domain/character_format.dart' show formatModifier, skillLabel;
import 'combat_support.dart';

CharacterController _controller(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

/// Animal companion (phase 25, block 6): "Elegir compañero" while the feature
/// is reached and no beast is chosen; afterwards the companion's statblock
/// with hit point controls, AC, saves, skills and attack/damage rolls, every
/// value with its breakdown. Nothing when the character has no companion
/// feature.
class CompanionSection extends ConsumerWidget {
  const CompanionSection({
    super.key,
    required this.character,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;
  final bool canEdit;
  final bool isDm;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = character;
    final feature = c.companionFeature;
    final companion = c.companion;
    if (companion != null) {
      return CompanionCard(character: c, companion: companion, canEdit: canEdit, isDm: isDm);
    }
    if (feature == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return CombatCard(
      key: const Key('companion-pending'),
      title: 'Compañero animal',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${feature.featureName}: elige una bestia (${feature.filterText}).',
            style: theme.textTheme.bodyMedium,
          ),
          const SizedBox(height: 8),
          SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              key: const Key('companion-choose'),
              onPressed: canEdit ? () => chooseCompanion(context, ref, c) : null,
              icon: const AppIcon(AppIcons.ranger, size: 20),
              label: const Text('Elegir compañero'),
            ),
          ),
        ],
      ),
    );
  }
}

/// Opens the beast picker filtered by the companion feature, asks for a name
/// and saves the choice. A player changing the beast of an active character
/// sends it to the DM.
Future<void> chooseCompanion(BuildContext context, WidgetRef ref, CharacterDetail character) async {
  final feature = character.companionFeature;
  if (feature == null) return;
  final beast = await Navigator.of(context)
      .push<BeastSummary>(MaterialPageRoute(builder: (_) => CompanionPickerPage(feature: feature)));
  if (beast == null || !context.mounted) return;
  final current = character.companion;
  final name = await showDialog<String>(
    context: context,
    builder: (_) =>
        CompanionNameDialog(initial: current?.name ?? beast.name, beastName: beast.name),
  );
  if (name == null || !context.mounted) return;
  SheetSaveResult? result;
  final done = await runCombat(context, () async {
    result = await _controller(ref, character).setCompanion(beastIndex: beast.index, name: name);
  });
  if (!done || !context.mounted) return;
  showCombatMessage(context, switch (result) {
    PendingApproval() => 'Enviado al DM para aprobación',
    _ => '${beast.name} es ahora tu compañero.',
  });
}

/// The beasts of the catalog the companion feature allows (challenge rating
/// and size); tapping one returns it.
class CompanionPickerPage extends ConsumerWidget {
  const CompanionPickerPage({super.key, required this.feature});

  final CompanionFeature feature;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final BeastQuery query = (maxCr: feature.maxChallengeRating, fly: null, swim: null);
    final beasts = ref.watch(beastsProvider(query));
    return Scaffold(
      appBar: AppBar(title: const Text('Elegir compañero')),
      body: beasts.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: ListTile(
            title: Text(describeApiError(error)),
            trailing: IconButton(
              tooltip: 'Reintentar',
              icon: const Icon(Icons.refresh),
              onPressed: () => ref.invalidate(beastsProvider(query)),
            ),
          ),
        ),
        data: (list) {
          final allowed = [
            for (final b in list)
              if (feature.allows(b.challengeRating, b.size)) b,
          ];
          return ListView(
            key: const Key('companion-picker'),
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                child: Text(
                  'Bestias de ${feature.filterText}.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
              if (allowed.isEmpty)
                const ListTile(title: Text('No hay bestias que cumplan el filtro.'))
              else
                for (final b in allowed)
                  ListTile(
                    key: Key('companion-beast-${b.index}'),
                    title: Text(b.name),
                    subtitle: Text(
                      [
                        companionSizeLabels[b.size] ?? b.size,
                        'VD ${b.challengeRatingText}',
                        'CA ${b.armorClass}',
                        '${b.hitPoints} PG',
                        formatBeastSpeeds(b.speeds),
                      ].where((e) => e.isNotEmpty).join(' · '),
                    ),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.of(context).pop(b),
                  ),
            ],
          );
        },
      ),
    );
  }
}

/// Asks for the companion's name (prefilled with [initial]).
class CompanionNameDialog extends StatefulWidget {
  const CompanionNameDialog({super.key, required this.initial, this.beastName});

  final String initial;
  final String? beastName;

  @override
  State<CompanionNameDialog> createState() => _CompanionNameDialogState();
}

class _CompanionNameDialogState extends State<CompanionNameDialog> {
  late final _name = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  void _submit() {
    final name = _name.text.trim();
    if (name.isEmpty) return;
    Navigator.of(context).pop(name);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.beastName == null ? 'Nombre del compañero' : 'Tu ${widget.beastName}'),
      content: TextField(
        key: const Key('companion-name'),
        controller: _name,
        autofocus: true,
        maxLength: 100,
        decoration: const InputDecoration(labelText: 'Nombre'),
        onSubmitted: (_) => _submit(),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('companion-name-save'),
          onPressed: _submit,
          child: const Text('Guardar'),
        ),
      ],
    );
  }
}

/// The companion's statblock in the Combat tab.
class CompanionCard extends ConsumerStatefulWidget {
  const CompanionCard({
    super.key,
    required this.character,
    required this.companion,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;
  final CharacterCompanion companion;
  final bool canEdit;
  final bool isDm;

  @override
  ConsumerState<CompanionCard> createState() => _CompanionCardState();
}

enum _CompanionAction { rename, change, remove }

class _CompanionCardState extends ConsumerState<CompanionCard> {
  final _amount = TextEditingController(text: '1');

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  int? _readAmount() {
    final value = int.tryParse(_amount.text.trim());
    if (value == null || value < 1) {
      showCombatMessage(context, 'Escribe una cantidad mayor que 0.');
      return null;
    }
    return value;
  }

  Future<void> _track(int sign) async {
    final amount = _readAmount();
    if (amount == null) return;
    await runCombat(
      context,
      () => _controller(ref, widget.character).trackCompanionHp(delta: sign * amount),
    );
  }

  Future<void> _menu(_CompanionAction action) async {
    final c = widget.character;
    switch (action) {
      case _CompanionAction.change:
        await chooseCompanion(context, ref, c);
      case _CompanionAction.rename:
        final name = await showDialog<String>(
          context: context,
          builder: (_) => CompanionNameDialog(initial: widget.companion.name),
        );
        if (name == null || name == widget.companion.name || !mounted) return;
        await runCombat(
          context,
          () =>
              _controller(ref, c).setCompanion(beastIndex: widget.companion.beastIndex, name: name),
          success: 'Compañero renombrado.',
        );
      case _CompanionAction.remove:
        final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Quitar compañero'),
            content: Text('${widget.companion.name} deja de acompañar a ${c.name}.'),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('Quitar'),
              ),
            ],
          ),
        );
        if (confirmed != true || !mounted) return;
        await runCombat(
          context,
          () => _controller(ref, c).deleteCompanion(),
          success: 'Compañero quitado.',
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final p = widget.companion;
    final max = p.hitPointsMax;
    final fraction = max <= 0 ? 0.0 : (p.hitPointsCurrent / max).clamp(0.0, 1.0);
    final barColor = fraction > .5
        ? tokens.moss
        : fraction > .25
        ? tokens.ember
        : tokens.blood;
    final bigNumber = numericStyle(theme.textTheme.headlineMedium);
    return CombatCard(
      key: const Key('companion-card'),
      title: 'Compañero: ${p.name}',
      trailing: widget.canEdit
          ? PopupMenuButton<_CompanionAction>(
              key: const Key('companion-menu'),
              tooltip: 'Opciones del compañero',
              onSelected: _menu,
              itemBuilder: (_) => [
                const PopupMenuItem(value: _CompanionAction.rename, child: Text('Renombrar')),
                if (widget.character.companionFeature != null)
                  const PopupMenuItem(
                    value: _CompanionAction.change,
                    child: Text('Cambiar bestia'),
                  ),
                if (widget.isDm)
                  const PopupMenuItem(
                    value: _CompanionAction.remove,
                    child: Text('Quitar compañero'),
                  ),
              ],
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            [
              p.beastName,
              if (p.size.isNotEmpty) companionSizeLabels[p.size] ?? p.size,
              'VD ${p.challengeRatingText}',
              if (p.speeds.isNotEmpty) formatBeastSpeeds(p.speeds),
            ].join(' · '),
            key: const Key('companion-beast'),
            style: theme.textTheme.bodySmall,
          ),
          if (p.beastMissing)
            Text(
              'La bestia ya no está en el catálogo.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
            ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text('${p.hitPointsCurrent} / ', key: const Key('companion-hp'), style: bigNumber),
              StatValue(
                statKey: 'companion.hitPointsMax',
                text: '$max',
                title: 'PG máximos de ${p.name}',
                breakdown: p.breakdowns['hitPointsMax'],
                style: bigNumber,
              ),
            ],
          ),
          const SizedBox(height: 6),
          LinearProgressIndicator(
            key: const Key('companion-hp-bar'),
            value: fraction,
            minHeight: 10,
            borderRadius: BorderRadius.circular(5),
            color: barColor,
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              IconButton.filledTonal(
                key: const Key('companion-hp-minus'),
                tooltip: 'Daño al compañero',
                onPressed: widget.canEdit ? () => _track(-1) : null,
                icon: const AppIcon(AppIcons.splash, size: 26),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: TextField(
                  key: const Key('companion-hp-amount'),
                  controller: _amount,
                  textAlign: TextAlign.center,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Daño / curación',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              IconButton.filledTonal(
                key: const Key('companion-hp-plus'),
                tooltip: 'Curar al compañero',
                onPressed: widget.canEdit ? () => _track(1) : null,
                icon: const AppIcon(AppIcons.drop, size: 26),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 16,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              _Fact(
                label: 'CA',
                child: StatValue(
                  statKey: 'companion.armorClass',
                  text: '${p.armorClass}',
                  title: 'CA de ${p.name}',
                  breakdown: p.breakdowns['armorClass'],
                  style: theme.textTheme.titleMedium,
                ),
              ),
              _Fact(
                label: 'Percepción pasiva',
                child: Text('${p.passivePerception}', style: theme.textTheme.titleMedium),
              ),
            ],
          ),
          if (p.savingThrows.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text('Salvaciones', style: theme.textTheme.labelLarge),
            Wrap(
              spacing: 12,
              children: [
                for (final e in p.savingThrows.entries)
                  _Fact(
                    label: abilityLabel(e.key),
                    child: StatValue(
                      statKey: 'companion.save.${e.key}',
                      text: formatModifier(e.value),
                      title: 'Salvación de ${abilityLabel(e.key)}',
                      breakdown: p.breakdowns['save.${e.key}'],
                    ),
                  ),
              ],
            ),
          ],
          if (p.skills.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text('Habilidades', style: theme.textTheme.labelLarge),
            Wrap(
              spacing: 12,
              children: [
                for (final e in p.skills.entries)
                  _Fact(
                    label: skillLabel(e.key),
                    child: StatValue(
                      statKey: 'companion.skill.${e.key}',
                      text: formatModifier(e.value),
                      title: skillLabel(e.key),
                      breakdown: p.breakdowns['skill.${e.key}'],
                    ),
                  ),
              ],
            ),
          ],
          if (p.attacks.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text('Acciones', style: theme.textTheme.labelLarge),
            for (final (i, a) in p.attacks.indexed) _CompanionAttackTile(attack: a, position: i),
          ],
        ],
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.label, required this.child});

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('$label ', style: Theme.of(context).textTheme.bodySmall),
        child,
      ],
    );
  }
}

/// An action of the companion: its bonuses (tap for the breakdown) and the
/// beast page's roll chips with the character's bonus already added.
class _CompanionAttackTile extends StatelessWidget {
  const _CompanionAttackTile({required this.attack, required this.position});

  final CompanionAttack attack;
  final int position;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final a = attack;
    return Padding(
      key: Key('companion-action-$position'),
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(a.name, style: theme.textTheme.titleSmall)),
              if (a.attackBonus != null)
                StatValue(
                  statKey: 'companion.attack.$position',
                  text: formatModifier(a.attackBonus!),
                  title: '${a.name}: ataque',
                  breakdown: a.attackBreakdown,
                ),
              if (a.damageBreakdown != null) ...[
                const SizedBox(width: 8),
                StatValue(
                  statKey: 'companion.damage.$position',
                  text: 'daño ${formatModifier(a.damageBreakdown!.total)}',
                  title: '${a.name}: bonificador de daño',
                  breakdown: a.damageBreakdown,
                  totalText: formatModifier(a.damageBreakdown!.total),
                ),
              ],
            ],
          ),
          if (a.attackBonus == null && a.description.isNotEmpty)
            Text(a.description, style: theme.textTheme.bodySmall),
          if (a.attackBonus != null || a.damageExpression != null)
            BeastRollChips(
              name: a.name,
              attackBonus: a.attackBonus,
              damage: a.damageExpression,
              damageTypes: a.damage.map((d) => d.type).whereType<String>().toList(),
              keyPrefix: 'companion',
              position: position,
            ),
        ],
      ),
    );
  }
}
