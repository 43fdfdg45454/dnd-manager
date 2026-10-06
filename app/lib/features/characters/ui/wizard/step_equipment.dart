import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/textures.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/theme/typography.dart';
import '../../../catalog/data/catalog_controllers.dart';
import '../../../catalog/data/models.dart' hide Page;
import '../../../catalog/domain/catalog_format.dart';
import '../../../items/ui/item_search_list.dart';
import '../../data/character_wizard_controller.dart';
import '../../domain/character_format.dart';
import 'step_basics.dart' show WizardLoadError, stepPadding;

String _qtyName(StartingItem item) =>
    item.quantity > 1 ? '${item.quantity} × ${item.name}' : item.name;

/// Starting equipment: kit (fixed items, choices) or starting gold, plus the
/// free list of extra items.
class EquipmentStep extends ConsumerWidget {
  const EquipmentStep({super.key, required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final theme = Theme.of(context);
    final classText = state.classDetail?.startingEquipmentText;
    final backgroundText = state.background?.startingEquipmentText;
    final structured = state.hasStructuredEquipment;
    final gold = state.equipmentMode == EquipmentMode.gold;

    return ListView(
      key: const Key('step-equipment'),
      padding: stepPadding,
      children: [
        Text(
          structured
              ? 'Elige el equipo inicial o tira el oro inicial. Puedes añadir más objetos abajo.'
              : 'Opcional: añade ahora el equipo inicial o hazlo más tarde desde el inventario.',
          style: theme.textTheme.bodySmall,
        ),
        if (structured) ...[
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            children: [
              ChoiceChip(
                key: const Key('equipment-mode-kit'),
                avatar: const AppIcon(AppIcons.backpack, size: 18),
                label: const Text('Equipo de clase y trasfondo'),
                selected: !gold,
                onSelected: (_) => controller.setEquipmentMode(EquipmentMode.kit),
              ),
              ChoiceChip(
                key: const Key('equipment-mode-gold'),
                avatar: const AppIcon(AppIcons.coins, size: 18),
                label: const Text('Oro inicial'),
                selected: gold,
                onSelected: state.startingGold == null
                    ? null
                    : (_) => controller.setEquipmentMode(EquipmentMode.gold),
              ),
            ],
          ),
          if (gold) _GoldSection(args: args) else _KitSection(args: args),
        ] else ...[
          if (classText != null && classText.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text('Equipo de clase', style: theme.textTheme.titleSmall),
            Text(cleanText(classText), key: const Key('wizard-class-equipment')),
          ],
          if (backgroundText != null && backgroundText.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text('Equipo de trasfondo', style: theme.textTheme.titleSmall),
            Text(cleanText(backgroundText), key: const Key('wizard-background-equipment')),
          ],
        ],
        if (structured) _DefaultEquipmentList(args: args),
        const SectionHeader('Otros objetos', padding: EdgeInsets.only(top: 16, bottom: 4)),
        Text('Objetos que añades tú, además del equipo inicial.', style: theme.textTheme.bodySmall),
        if (state.equipment.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('Ningún objeto añadido.'),
          ),
        for (final line in state.equipment)
          EquipmentLineTile(
            key: Key('equipment-${line.templateId}'),
            name: line.name,
            quantity: line.qty,
            trailing: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                IconButton(
                  key: Key('equipment-minus-${line.templateId}'),
                  tooltip: 'Restar',
                  onPressed: () => controller.setEquipmentQuantity(line.templateId, line.qty - 1),
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                IconButton(
                  key: Key('equipment-plus-${line.templateId}'),
                  tooltip: 'Sumar',
                  onPressed: () => controller.setEquipmentQuantity(line.templateId, line.qty + 1),
                  icon: const Icon(Icons.add_circle_outline),
                ),
                IconButton(
                  key: Key('equipment-remove-${line.templateId}'),
                  tooltip: 'Quitar',
                  onPressed: () => controller.removeEquipment(line.templateId),
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
          ),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerLeft,
          child: OutlinedButton.icon(
            key: const Key('wizard-add-item'),
            onPressed: () =>
                Navigator.of(context)
                    .push<void>(MaterialPageRoute(builder: (_) => _ItemPickerPage(args: args))),
            icon: const Icon(Icons.add),
            label: const Text('Añadir otro objeto'),
          ),
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Kit
// ---------------------------------------------------------------------------

class _KitSection extends ConsumerWidget {
  const _KitSection({required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return _ChoiceList(args: args);
  }
}

/// One item line of the equipment step. The same look for the default
/// starting items and the ones the player adds, so both read as "my items".
class EquipmentLineTile extends StatelessWidget {
  const EquipmentLineTile({
    super.key,
    required this.name,
    required this.quantity,
    this.trailing,
    this.contents,
  });

  final String name;
  final int quantity;
  final Widget? trailing;

  /// Items inside a pack (explorer's pack…); the line expands to show them.
  final List<StartingItem>? contents;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = Text(name);
    final subtitle = Text('Cantidad: $quantity');
    final inner = contents;
    if (inner == null || inner.isEmpty) {
      return ListTile(
        contentPadding: EdgeInsets.zero,
        title: title,
        subtitle: subtitle,
        trailing: trailing,
      );
    }
    return ExpansionTile(
      tilePadding: EdgeInsets.zero,
      title: title,
      subtitle: subtitle,
      trailing: trailing,
      children: [
        for (final c in inner)
          ListTile(
            dense: true,
            contentPadding: const EdgeInsets.only(left: 16),
            title: Text(_qtyName(c), style: theme.textTheme.bodySmall),
          ),
      ],
    );
  }
}

/// "Equipo inicial": the items the class and background give with the
/// current choices (or the background kit kept with starting gold), shown
/// like the player's own items under their own heading.
class _DefaultEquipmentList extends ConsumerWidget {
  const _DefaultEquipmentList({required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final lines = state.startingLines;
    final copper = state.startingCopper;

    // Pack contents by template, to let packs expand.
    final contents = <String, List<StartingItem>>{};
    void collect(StartingItem i) {
      final id = i.templateId;
      if (id != null && i.contents != null && i.contents!.isNotEmpty) contents[id] = i.contents!;
    }

    for (final e in [state.classEquipment, state.backgroundEquipment]) {
      if (e == null) continue;
      e.fixed.forEach(collect);
      for (final c in e.choices) {
        for (final o in c.options) {
          o.items.forEach(collect);
        }
      }
    }

    final badge = Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        border: Border.all(color: tokens.oldGold.withValues(alpha: 0.6)),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        'Por defecto',
        style: theme.textTheme.labelSmall?.copyWith(color: tokens.oldGold),
      ),
    );

    return Column(
      key: const Key('equipment-included'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Equipo inicial', padding: EdgeInsets.only(top: 16, bottom: 4)),
        Text(
          'Lo que te dan tu clase y tu trasfondo con las elecciones de arriba.',
          style: theme.textTheme.bodySmall,
        ),
        if (lines.isEmpty && copper <= 0)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('Completa las elecciones para ver tu equipo.'),
          ),
        for (final line in lines)
          EquipmentLineTile(
            key: Key('equipment-default-${line.templateId}'),
            name: line.name,
            quantity: line.qty,
            contents: contents[line.templateId],
            trailing: badge,
          ),
        if (copper > 0)
          ListTile(
            key: const Key('equipment-default-gold'),
            contentPadding: EdgeInsets.zero,
            title: Text('${copperToGoldText(copper)} po'),
            subtitle: const Text('Oro inicial'),
            trailing: badge,
          ),
      ],
    );
  }
}

class _ChoiceList extends ConsumerWidget {
  const _ChoiceList({required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final active = state.activeChoiceIndexes;
    final choices = state.allEquipmentChoices;
    if (active.isEmpty) return const SizedBox.shrink();

    void openPicker(int choice, int option, int category, StartingCategoryPick pick) {
      Navigator.of(context).push<void>(
        MaterialPageRoute(
          builder: (_) => _CategoryPickerPage(
            args: args,
            pickKey: categoryPickKey(choice, option, category),
            pick: pick,
          ),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(
          'Elecciones',
          padding: const EdgeInsets.only(top: 16, bottom: 4),
          trailing: Text(
            'Elecciones ${state.completedChoices} de ${active.length}',
            key: const Key('equipment-choices-counter'),
            style: AppTypography.numeric.copyWith(color: tokens.boneMuted),
          ),
        ),
        for (final i in active)
          Column(
            key: Key('equipment-choice-$i'),
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.only(top: 12, bottom: 2),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(choices[i].description, style: theme.textTheme.titleSmall),
                    ),
                    if (state.isChoiceComplete(i)) Icon(Icons.check, size: 18, color: tokens.moss),
                  ],
                ),
              ),
              for (var j = 0; j < choices[i].options.length; j++)
                _OptionCard(
                  key: Key('equipment-option-$i-$j'),
                  keyPrefix: '$i-$j',
                  option: choices[i].options[j],
                  selected: state.equipmentOptions[i]?.contains(j) ?? false,
                  picks: [
                    for (var k = 0; k < choices[i].options[j].categories.length; k++)
                      state.categoryPicks[categoryPickKey(i, j, k)] ?? const [],
                  ],
                  onTap: () {
                    controller.selectEquipmentOption(i, j);
                    final option = choices[i].options[j];
                    final current = ref.read(characterWizardControllerProvider(args));
                    for (var k = 0; k < option.categories.length; k++) {
                      final done = current.categoryPicks[categoryPickKey(i, j, k)]?.length ?? 0;
                      if (done != option.categories[k].choose) {
                        openPicker(i, j, k, option.categories[k]);
                        break;
                      }
                    }
                  },
                  onPick: (k) => openPicker(i, j, k, choices[i].options[j].categories[k]),
                ),
            ],
          ),
      ],
    );
  }
}

class _OptionCard extends StatelessWidget {
  const _OptionCard({
    super.key,
    required this.keyPrefix,
    required this.option,
    required this.selected,
    required this.picks,
    required this.onTap,
    required this.onPick,
  });

  final String keyPrefix;
  final StartingEquipmentOption option;
  final bool selected;
  final List<List<EquipmentCategoryItem>> picks;
  final VoidCallback onTap;
  final void Function(int category) onPick;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    return RuneCard(
      margin: const EdgeInsets.symmetric(vertical: 4),
      padding: const EdgeInsets.all(12),
      borderColor: selected ? tokens.ember : null,
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            selected ? Icons.radio_button_checked : Icons.radio_button_unchecked,
            color: selected ? tokens.ember : tokens.boneMuted,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(option.label, style: theme.textTheme.titleSmall),
                for (final item in option.items)
                  Text(_qtyName(item), style: theme.textTheme.bodySmall),
                for (var k = 0; k < option.categories.length; k++)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: selected
                        ? OutlinedButton(
                            key: Key('equipment-pick-$keyPrefix-$k'),
                            onPressed: () => onPick(k),
                            child: Text(
                              picks[k].isEmpty
                                  ? 'Elegir ${option.categories[k].choose}: '
                                        '${option.categories[k].name}'
                                  : '${picks[k].map((e) => e.name).join(', ')} '
                                        '(${picks[k].length}/${option.categories[k].choose})',
                            ),
                          )
                        : Text(
                            'Elegir ${option.categories[k].choose}: ${option.categories[k].name}',
                            style: theme.textTheme.bodySmall,
                          ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Items of an equipment category; exactly `pick.choose` must be chosen.
class _CategoryPickerPage extends ConsumerWidget {
  const _CategoryPickerPage({required this.args, required this.pickKey, required this.pick});

  final WizardArgs args;
  final String pickKey;
  final StartingCategoryPick pick;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final picked =
        ref.watch(
          characterWizardControllerProvider(args).select((s) => s.categoryPicks[pickKey]),
        ) ??
        const <EquipmentCategoryItem>[];
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final category = ref.watch(equipmentCategoryProvider(pick.category));
    return Scaffold(
      key: const Key('equipment-category-picker'),
      appBar: AppBar(
        title: Text(pick.name),
        actions: [
          TextButton(
            key: const Key('equipment-category-done'),
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Listo'),
          ),
        ],
      ),
      body: category.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => WizardLoadError(
          error: error,
          onRetry: () => ref.invalidate(equipmentCategoryProvider(pick.category)),
        ),
        data: (data) => Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                'Elige ${pick.choose}: ${picked.length} de ${pick.choose}',
                key: const Key('equipment-category-counter'),
              ),
            ),
            Expanded(
              child: ListView(
                children: [
                  for (final item in data.items)
                    CheckboxListTile(
                      key: Key('equipment-category-item-${item.index}'),
                      value: picked.any((e) => e.templateId == item.templateId),
                      title: Text(item.name),
                      onChanged: (_) => controller.toggleCategoryItem(pickKey, pick.choose, item),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Gold
// ---------------------------------------------------------------------------

class _GoldSection extends ConsumerWidget {
  const _GoldSection({required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(characterWizardControllerProvider(args));
    final controller = ref.read(characterWizardControllerProvider(args).notifier);
    final gold = state.startingGold!;
    final tokens = context.tokens;
    final range = state.goldRange;
    final roll = state.goldRoll;
    final background = state.backgroundEquipment;
    final outOfRange = roll != null && range != null && (roll < range.min || roll > range.max);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionHeader('Oro inicial', padding: EdgeInsets.only(top: 16, bottom: 8)),
        RuneCard(
          margin: const EdgeInsets.symmetric(vertical: 4),
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _GoldRollField(
                initial: roll,
                label: 'Tira ${gold.dice} y escribe el resultado',
                helper: range == null ? null : 'Entre ${range.min} y ${range.max}',
                error: outOfRange ? 'La tirada va de ${range.min} a ${range.max}' : null,
                onChanged: controller.setGoldRoll,
              ),
              if (roll != null && !outOfRange)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Row(
                    children: [
                      AppIcon(AppIcons.coins, color: tokens.oldGold, size: 20),
                      const SizedBox(width: 8),
                      Text(
                        '× ${gold.multiplier} = ${roll * gold.multiplier} po',
                        key: const Key('equipment-gold-preview'),
                        style: AppTypography.numeric.copyWith(color: tokens.oldGold),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
        if (background != null)
          CheckboxListTile(
            key: const Key('equipment-keep-background'),
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.leading,
            value: state.keepBackgroundEquipment,
            title: const Text('Conservar el equipo del trasfondo'),
            onChanged: (value) => controller.setKeepBackgroundEquipment(value ?? false),
          ),
        if (state.keepBackgroundEquipment && background != null) _ChoiceList(args: args),
      ],
    );
  }
}

class _GoldRollField extends StatefulWidget {
  const _GoldRollField({
    required this.initial,
    required this.label,
    required this.onChanged,
    this.helper,
    this.error,
  });

  final int? initial;
  final String label;
  final String? helper;
  final String? error;
  final ValueChanged<int?> onChanged;

  @override
  State<_GoldRollField> createState() => _GoldRollFieldState();
}

class _GoldRollFieldState extends State<_GoldRollField> {
  late final _controller = TextEditingController(text: widget.initial?.toString() ?? '');

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => TextField(
    key: const Key('equipment-gold-roll'),
    controller: _controller,
    keyboardType: TextInputType.number,
    inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(5)],
    decoration: InputDecoration(
      labelText: widget.label,
      helperText: widget.helper,
      errorText: widget.error,
    ),
    onChanged: (value) => widget.onChanged(int.tryParse(value)),
  );
}

// ---------------------------------------------------------------------------
// Free items
// ---------------------------------------------------------------------------

/// Item search that adds every tapped item to the wizard's equipment.
class _ItemPickerPage extends ConsumerWidget {
  const _ItemPickerPage({required this.args});

  final WizardArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lines = ref.watch(characterWizardControllerProvider(args).select((s) => s.equipment));
    return Scaffold(
      appBar: AppBar(
        title: const Text('Añadir objeto'),
        actions: [
          TextButton(
            key: const Key('wizard-item-done'),
            onPressed: () {
              ScaffoldMessenger.of(context).hideCurrentSnackBar();
              Navigator.of(context).pop();
            },
            child: Text(lines.isEmpty ? 'Listo' : 'Listo (${lines.length})'),
          ),
        ],
      ),
      body: ItemSearchList(
        campaignId: args.campaignId,
        onSelected: (item) {
          ref
              .read(characterWizardControllerProvider(args).notifier)
              .addEquipment(item.id, item.name);
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text('Añadido: ${item.name}')));
        },
      ),
    );
  }
}
