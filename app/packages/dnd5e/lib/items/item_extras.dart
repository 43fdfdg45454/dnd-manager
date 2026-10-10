import 'package:flutter/material.dart';
import 'package:opentrpg_core/features/items/data/models.dart';

import '../catalog/domain/catalog_format.dart' show rarityLabel;
import 'dnd5e_item.dart';

/// Small chips with the D&D 5e facts of an item: rarity, attunement, damage
/// and armor class. Nothing when the item has none.
class Dnd5eItemExtras extends StatelessWidget {
  const Dnd5eItemExtras({super.key, required this.item});

  final EffectiveItem item;

  @override
  Widget build(BuildContext context) {
    final damage = item.damage;
    final armorClass = item.armor?.baseAc;
    final labels = [
      if (item.rarity != null) rarityLabel(item.rarity),
      if (item.requiresAttunement) 'Sintonización',
      if (damage != null) [damage.dice, ?damage.type].join(' '),
      if (armorClass != null) 'CA $armorClass',
    ];
    if (labels.isEmpty) return const SizedBox.shrink();
    return Wrap(
      spacing: 4,
      runSpacing: 4,
      children: [
        for (final label in labels)
          Chip(label: Text(label), visualDensity: VisualDensity.compact, padding: EdgeInsets.zero),
      ],
    );
  }
}
