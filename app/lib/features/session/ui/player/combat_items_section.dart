import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/ui/offline_widgets.dart';
import '../../../catalog/domain/catalog_format.dart';
import '../../../characters/data/characters_controller.dart';
import '../../../characters/data/models.dart' show CharacterDetail;
import '../../../characters/ui/combat/combat_support.dart';
import '../../../items/data/items_controllers.dart';
import '../../../items/data/models.dart';
import '../../../items/domain/combat_usable.dart';
import '../../../items/domain/items_format.dart';

/// The consumables of the combat view of "Mi sesión": the inventory items that
/// matter in a fight (see [isCombatUsable]: weapons, shields, armor,
/// consumables, items with charges or attack and damage bonuses), with a
/// "Usar" button for the ones that can be spent.
class CombatItemsSection extends ConsumerWidget {
  const CombatItemsSection({super.key, required this.character, required this.canEdit});

  final CharacterDetail character;
  final bool canEdit;

  Future<void> _use(BuildContext context, WidgetRef ref, CharacterItem item) async {
    final inventory = ref.read(inventoryControllerProvider(character.id).notifier);
    final sheet = ref.read(characterControllerProvider(character.id).notifier);
    await runCombat(context, () async {
      await inventory.use(item.id);
      await sheet.reload();
    }, success: 'Usado: ${item.effective.name}.');
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final inventory = ref.watch(inventoryControllerProvider(character.id));
    return CombatCard(
      title: 'Objetos de combate',
      child: inventory.when(
        skipLoadingOnReload: true,
        loading: () => const Padding(
          padding: EdgeInsets.all(12),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (_, _) => TextButton(
          onPressed: () => ref.invalidate(inventoryControllerProvider(character.id)),
          child: const Text('No se pudo cargar el inventario. Reintentar'),
        ),
        data: (data) {
          final items = data.items.where(isCombatUsableItem).toList();
          if (items.isEmpty) {
            return const Text('Sin objetos de combate.', key: Key('combat-items-empty'));
          }
          return Column(
            key: const Key('combat-items'),
            children: [
              for (final item in items)
                ListTile(
                  key: Key('combat-item-${item.id}'),
                  contentPadding: EdgeInsets.zero,
                  title: Text(item.effective.name),
                  subtitle: Text(
                    [
                      itemCategoryLabel(item.effective.category),
                      if (item.equipped) 'Equipado',
                      if (item.charges != null)
                        'Cargas ${item.charges}/${item.chargesMax ?? item.charges}',
                      if (item.quantity > 1) 'Cantidad ${item.quantity}',
                    ].join(' · '),
                  ),
                  trailing: canUse(item)
                      ? OfflineAware(
                          builder: (context, canWrite) => FilledButton.tonal(
                            key: Key('combat-item-use-${item.id}'),
                            onPressed: canEdit && canWrite ? () => _use(context, ref, item) : null,
                            child: const Text('Usar'),
                          ),
                        )
                      : null,
                ),
            ],
          );
        },
      ),
    );
  }
}
