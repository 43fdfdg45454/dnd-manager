import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/ui/source_chip.dart';
import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

/// One equipment or magic item.
class ItemDetailPage extends ConsumerWidget {
  const ItemDetailPage({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final item = ref.watch(itemDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: Text(item.value?.name ?? 'Objeto')),
      body: CatalogAsyncBody<ItemDetail>(
        value: item,
        onRetry: () => ref.invalidate(itemDetailProvider(id)),
        builder: (i) => DetailList(
          children: [
            Align(alignment: Alignment.centerLeft, child: SourceChip(i.source)),
            Text(
              [
                itemCategoryLabel(i.category),
                ?i.subcategory,
              ].where((e) => e.isNotEmpty).join(' · '),
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            FactRow('Rareza', i.rarity == null ? null : rarityLabel(i.rarity)),
            if (i.requiresAttunement) const FactRow('Sintonización', 'Requerida'),
            FactRow('Coste', formatCostCp(i.costCp)),
            FactRow('Peso', formatWeightLb(i.weightLb)),
            FactRow('Daño', _damage(i.damage)),
            FactRow('Versátil', i.damage?.versatileDice),
            FactRow('Alcance', _range(i)),
            FactRow('Propiedades', i.properties.join(', ')),
            FactRow('Clase de armadura', _armorClass(i.armor)),
            FactRow('Fuerza mínima', i.armor?.strengthMinimum?.toString()),
            if (i.armor?.stealthDisadvantage ?? false) const FactRow('Sigilo', 'Desventaja'),
            if (i.modifiers.isNotEmpty || i.effects.isNotEmpty) ...[
              const SectionTitle('Efectos'),
              ModifierLines(i.modifiers),
              Paragraphs(i.effects),
            ],
            if (i.description.isNotEmpty) ...[
              const SectionTitle('Descripción'),
              Paragraphs(i.description),
            ],
          ],
        ),
      ),
    );
  }

  static String? _damage(ItemDamage? damage) {
    if (damage == null) return null;
    return [damage.dice, ?damage.type].join(' ');
  }

  static String? _range(ItemDetail i) {
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
}
