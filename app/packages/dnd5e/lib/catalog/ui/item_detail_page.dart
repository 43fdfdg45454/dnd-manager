import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:opentrpg_core/core/ui/source_chip.dart';

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
            if (i.isTool) const FactRow('Tipo', 'Herramienta'),
            if (i.isAmmunition) const FactRow('Tipo', 'Munición'),
            if (i.firearmMisfire != null) ...[
              FactRow(
                'Recarga',
                i.firearmReload == null
                    ? null
                    : '${i.firearmReload} ${i.firearmReload == 1 ? 'disparo' : 'disparos'}',
              ),
              FactRow(
                'Fallo',
                i.firearmMisfire == 1 ? '1 en el d20' : '1–${i.firearmMisfire} en el d20',
              ),
            ],
            FactRow('Especial', i.special),
            if (i.properties.isNotEmpty) _PropertyTexts(properties: i.properties),
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

/// The rules text of the weapon properties of an item that have one in the
/// vocabulary of the catalog (the new properties of the content packs, such as
/// "misfire", arrive with their text).
class _PropertyTexts extends ConsumerWidget {
  const _PropertyTexts({required this.properties});

  final List<String> properties;

  static String _key(String value) => value.toLowerCase().replaceAll(RegExp(r'[^a-z0-9]'), '');

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(referenceProvider(ReferenceKinds.weaponProperties)).value;
    if (entries == null || entries.isEmpty) return const SizedBox.shrink();
    final byKey = {
      for (final e in entries.values) ...{_key(e.index): e, _key(e.name): e},
    };
    final described = [
      for (final p in properties)
        if (byKey[_key(p)] case final entry? when entry.description.isNotEmpty) entry,
    ];
    if (described.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final e in described)
          ExpandableEntry(
            key: Key('item-property-${e.index}'),
            title: e.name,
            description: e.description,
          ),
      ],
    );
  }
}
