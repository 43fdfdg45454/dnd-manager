import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../characters/domain/character_format.dart' show skillLabel;
import '../../dice/domain/dice_expression.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/beast_models.dart';
import '../data/catalog_controllers.dart';
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

const _abilityOrder = ['str', 'dex', 'con', 'int', 'wis', 'cha'];

const _abilityShort = {
  'str': 'FUE',
  'dex': 'DES',
  'con': 'CON',
  'int': 'INT',
  'wis': 'SAB',
  'cha': 'CAR',
};

const _senseLabels = {
  'darkvision': 'Visión en la oscuridad',
  'blindsight': 'Vista ciega',
  'tremorsense': 'Sentido sísmico',
  'truesight': 'Visión verdadera',
};

String _signed(int value) => value >= 0 ? '+$value' : '$value';

int _modifier(int score) => ((score - 10) / 2).floor();

/// One row of a beast list: name, size, CR, AC, HP and speeds; opens the
/// statblock.
class BeastTile extends StatelessWidget {
  const BeastTile({super.key, required this.beast});

  final BeastSummary beast;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      key: Key('beast-${beast.index}'),
      title: Text(beast.name),
      subtitle: Text(
        [
          'VD ${beast.challengeRatingText}',
          'CA ${beast.armorClass}',
          '${beast.hitPoints} PG',
          formatBeastSpeeds(beast.speeds),
        ].where((e) => e.isNotEmpty).join(' · '),
      ),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => context.push(AppRoutes.beast(beast.index)),
    );
  }
}

/// Statblock of an SRD beast: characteristics, AC, HP, speeds, senses, traits
/// and actions whose attack and damage can be rolled.
class BeastPage extends ConsumerWidget {
  const BeastPage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(beastDetailProvider(index));
    return Scaffold(
      appBar: AppBar(title: Text(detail.value?.name ?? 'Bestia')),
      body: CatalogAsyncBody<Beast>(
        value: detail,
        onRetry: () => ref.invalidate(beastDetailProvider(index)),
        builder: (b) => DetailList(
          children: [
            Text(
              [b.size, 'bestia', if (b.alignment.isNotEmpty) b.alignment].join(' · '),
              key: const Key('beast-type'),
              style: Theme.of(context).textTheme.bodySmall,
            ),
            FactRow('Desafío', 'VD ${b.challengeRatingText} (${b.xp} PX)'),
            FactRow(
              'Clase de armadura',
              b.armorClassType == 'natural'
                  ? '${b.armorClass} (armadura natural)'
                  : '${b.armorClass}',
            ),
            FactRow(
              'Puntos de golpe',
              b.hitPointsRoll == null ? '${b.hitPoints}' : '${b.hitPoints} (${b.hitPointsRoll})',
            ),
            FactRow('Velocidad', formatBeastSpeeds(b.speeds)),
            const SizedBox(height: 8),
            _AbilityRow(abilities: b.abilities),
            const SizedBox(height: 8),
            if (b.savingThrows.isNotEmpty)
              FactRow(
                'Salvaciones',
                [
                  for (final e in b.savingThrows.entries)
                    '${abilityLabel(e.key)} ${_signed(e.value)}',
                ].join(', '),
              ),
            if (b.skills.isNotEmpty)
              FactRow(
                'Habilidades',
                [for (final e in b.skills.entries) '${skillLabel(e.key)} ${_signed(e.value)}']
                    .join(', '),
              ),
            if (b.damageVulnerabilities.isNotEmpty)
              FactRow('Vulnerabilidades', b.damageVulnerabilities.join(', ')),
            if (b.damageResistances.isNotEmpty)
              FactRow('Resistencias', b.damageResistances.join(', ')),
            if (b.damageImmunities.isNotEmpty)
              FactRow('Inmunidades al daño', b.damageImmunities.join(', ')),
            if (b.conditionImmunities.isNotEmpty)
              FactRow('Inmunidades a condiciones', b.conditionImmunities.join(', ')),
            FactRow(
              'Sentidos',
              [
                for (final e in b.senses.entries) '${_senseLabels[e.key] ?? e.key} ${e.value}',
                'Percepción pasiva ${b.passivePerception}',
              ].join(', '),
            ),
            if (b.traits.isNotEmpty) ...[
              const SectionTitle('Rasgos'),
              for (final t in b.traits)
                ExpandableEntry(
                  key: Key('beast-trait-${t.name}'),
                  title: t.name,
                  subtitle: t.save == null
                      ? null
                      : 'CD ${t.save!.dc} de ${abilityLabel(t.save!.ability)}',
                  description: [t.description],
                ),
            ],
            if (b.actions.isNotEmpty) ...[
              const SectionTitle('Acciones'),
              for (final (i, a) in b.actions.indexed) _ActionCard(action: a, position: i),
            ],
            if (b.description != null) ...[const SectionTitle('Descripción'), Text(b.description!)],
            const SizedBox(height: 12),
            Text(
              'Contenido del SRD 5.1 (CC-BY 4.0). Los textos de las bestias están en inglés.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}

class _AbilityRow extends StatelessWidget {
  const _AbilityRow({required this.abilities});

  final Map<String, int> abilities;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      key: const Key('beast-abilities'),
      children: [
        for (final a in _abilityOrder)
          Expanded(
            child: Column(
              children: [
                Text(_abilityShort[a]!, style: theme.textTheme.labelSmall),
                Text('${abilities[a] ?? 10}', style: theme.textTheme.titleMedium),
                Text(_signed(_modifier(abilities[a] ?? 10)), style: theme.textTheme.bodySmall),
              ],
            ),
          ),
      ],
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({required this.action, required this.position});

  final BeastAction action;
  final int position;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final bonus = action.attackBonus;
    final damage = action.damageExpression;
    final save = action.save;
    return Card(
      key: Key('beast-action-$position'),
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 10, 12, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(action.name, style: theme.textTheme.titleSmall),
            const SizedBox(height: 4),
            Text(action.description, style: theme.textTheme.bodySmall),
            if (bonus != null || damage != null || save != null) ...[
              const SizedBox(height: 6),
              BeastRollChips(
                name: action.name,
                attackBonus: bonus,
                damage: damage,
                damageTypes: action.damage.map((d) => d.type).whereType<String>().toList(),
                keyPrefix: 'beast',
                position: position,
                extra: [
                  if (save != null)
                    Chip(
                      key: Key('beast-save-$position'),
                      label: Text('CD ${save.dc} de ${abilityLabel(save.ability)}'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Attack and damage roll chips of a beast action ("Ataque +4", "Daño 2d4+2"),
/// shared by the beast statblock and the animal companion of the Combat tab.
/// Keys are `<keyPrefix>-attack-<position>` and `<keyPrefix>-damage-<position>`.
class BeastRollChips extends StatelessWidget {
  const BeastRollChips({
    super.key,
    required this.name,
    required this.attackBonus,
    required this.damage,
    this.damageTypes = const [],
    required this.keyPrefix,
    required this.position,
    this.extra = const [],
  });

  final String name;
  final int? attackBonus;

  /// Damage expression ("2d4+2"), or null.
  final String? damage;
  final List<String> damageTypes;
  final String keyPrefix;
  final int position;

  /// More chips after the rolls (a saving throw...).
  final List<Widget> extra;

  @override
  Widget build(BuildContext context) {
    final bonus = attackBonus;
    final dice = damage;
    return Wrap(
      spacing: 8,
      runSpacing: 4,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        if (bonus != null)
          ActionChip(
            key: Key('$keyPrefix-attack-$position'),
            avatar: const Icon(Icons.casino_outlined, size: 18),
            label: Text('Ataque ${_signed(bonus)}'),
            onPressed: () => rollAndShow(
              context,
              '1d20${_signed(bonus)}',
              label: '$name: ataque',
              kind: RollKind.attack,
            ),
          ),
        if (dice != null)
          ActionChip(
            key: Key('$keyPrefix-damage-$position'),
            avatar: const Icon(Icons.casino_outlined, size: 18),
            label: Text('Daño $dice'),
            onPressed: () =>
                rollAndShow(context, dice, label: ['$name: daño', ...damageTypes].join(' · ')),
          ),
        ...extra,
      ],
    );
  }
}
