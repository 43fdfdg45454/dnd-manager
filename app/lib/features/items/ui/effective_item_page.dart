import 'package:flutter/material.dart';

import '../../catalog/data/models.dart' show ItemArmor, ItemDamage;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/ui/detail_widgets.dart';
import '../data/models.dart';

/// Icon flagging a field (or the whole item) as different from its template.
class OverrideBadge extends StatelessWidget {
  const OverrideBadge({super.key, this.tooltip = 'Modificado respecto a la plantilla'});

  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      triggerMode: TooltipTriggerMode.tap,
      child: Icon(Icons.edit_note, size: 18, color: Theme.of(context).colorScheme.primary),
    );
  }
}

/// Detail of an item as the character sees it (template plus overrides), with
/// the overridden fields marked. Reuses the catalog detail widgets.
class EffectiveItemPage extends StatelessWidget {
  const EffectiveItemPage({
    super.key,
    required this.effective,
    this.overrides = const ItemOverrides(),
    this.isCustom = false,
    this.extraFacts = const [],
  });

  final EffectiveItem effective;

  /// The fields that differ from the template.
  final ItemOverrides overrides;
  final bool isCustom;

  /// "label: value" rows shown first (quantity, charges, notes, ...).
  final List<(String, String?)> extraFacts;

  bool _marked(List<String> fields) => fields.any(overrides.definedFields.contains);

  Widget _row(String label, String? value, List<String> fields) {
    if (value == null || value.isEmpty) return const SizedBox.shrink();
    final marked = _marked(fields);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(child: FactRow(label, value)),
        if (marked)
          Padding(
            padding: const EdgeInsets.only(top: 3),
            child: KeyedSubtree(
              key: Key('override-mark-${fields.first}'),
              child: const OverrideBadge(),
            ),
          ),
      ],
    );
  }

  static String? _damage(ItemDamage? damage) =>
      damage == null ? null : [damage.dice, ?damage.type].join(' ');

  static String? _range(EffectiveItem i) {
    final normal = i.rangeNormal;
    if (normal == null) return null;
    final long = i.rangeLong;
    return long == null ? '$normal pies' : '$normal/$long pies';
  }

  static String? _armorClass(ItemArmor? armor) {
    final base = armor?.baseAc;
    if (armor == null || base == null) return null;
    if (armor.addDexModifier != true) return '$base';
    final max = armor.maxDexBonus;
    return max == null ? '$base + mod. Des' : '$base + mod. Des (máx. $max)';
  }

  static String? _bonus(int value) => value == 0 ? null : (value > 0 ? '+$value' : '$value');

  @override
  Widget build(BuildContext context) {
    final i = effective;
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(i.name, key: const Key('effective-title'))),
      body: DetailList(
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  [
                    itemCategoryLabel(i.category),
                    ?i.subcategory,
                  ].where((e) => e.isNotEmpty).join(' · '),
                  style: theme.textTheme.titleSmall,
                ),
              ),
              if (_marked(const ['category', 'name']))
                KeyedSubtree(key: const Key('override-mark-name'), child: const OverrideBadge()),
            ],
          ),
          if (isCustom || !overrides.isEmpty) ...[
            const SizedBox(height: 4),
            Text(
              overrides.isEmpty
                  ? 'Objeto personalizado, sin plantilla.'
                  : 'Los campos marcados con el icono se han modificado respecto a la plantilla.',
              key: const Key('effective-note'),
              style: theme.textTheme.bodySmall,
            ),
          ],
          const SizedBox(height: 8),
          for (final (label, value) in extraFacts) FactRow(label, value),
          _row('Rareza', i.rarity == null ? null : rarityLabel(i.rarity), const ['rarity']),
          _row('Sintonización', i.requiresAttunement ? 'Requerida' : null, const [
            'requiresAttunement',
          ]),
          _row('Peso', i.weightLb == null ? null : formatWeightLb(i.weightLb), const ['weightLb']),
          _row('Daño', _damage(i.damage), const ['damageDice', 'damageType']),
          _row('Versátil', i.damage?.versatileDice, const ['versatileDice']),
          _row('Alcance', _range(i), const ['rangeNormal', 'rangeLong']),
          _row('Propiedades', i.properties.join(', '), const ['properties']),
          _row('Clase de armadura', _armorClass(i.armor), const [
            'armorClassBase',
            'addDexModifier',
            'maxDexBonus',
          ]),
          _row('Fuerza mínima', i.armor?.strengthMinimum?.toString(), const ['strengthMinimum']),
          _row('Sigilo', (i.armor?.stealthDisadvantage ?? false) ? 'Desventaja' : null, const [
            'stealthDisadvantage',
          ]),
          _row('Bono de ataque', _bonus(i.attackBonus), const ['attackBonus']),
          _row('Bono de daño', _bonus(i.damageBonus), const ['damageBonus']),
          if (i.effects.isNotEmpty) ...[
            Row(
              children: [
                const Expanded(child: SectionTitle('Efectos')),
                if (_marked(const ['effects']))
                  KeyedSubtree(
                    key: const Key('override-mark-effects'),
                    child: const OverrideBadge(),
                  ),
              ],
            ),
            Paragraphs(i.effects),
          ],
          if (i.description.isNotEmpty) ...[
            Row(
              children: [
                const Expanded(child: SectionTitle('Descripción')),
                if (_marked(const ['description']))
                  KeyedSubtree(
                    key: const Key('override-mark-description'),
                    child: const OverrideBadge(),
                  ),
              ],
            ),
            Paragraphs(i.description),
          ],
        ],
      ),
    );
  }
}
