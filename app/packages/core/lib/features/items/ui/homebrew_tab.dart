import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../content_packs/ui/campaign_content_packs_section.dart';
import '../data/campaign_items_repository.dart';
import '../data/items_controllers.dart';
import '../data/models.dart' show ItemSummary;
import 'item_feedback.dart';
import 'item_search_list.dart';

/// "Contenido" section of a campaign: the SRD and homebrew items with search
/// and a source toggle. At least a DM creates, edits and deletes the homebrew
/// ones; the players also see, read-only, the content packs the campaign
/// enables (the DMs change them in "Ajustes").
class HomebrewTab extends ConsumerWidget {
  const HomebrewTab({super.key, required this.campaign});

  final CampaignDetail campaign;

  Future<void> _delete(BuildContext context, WidgetRef ref, ItemSummary item) async {
    final confirmed = await confirmAction(
      context,
      title: 'Borrar objeto',
      message: '¿Seguro que quieres borrar "${item.name}"? No se puede deshacer.',
      confirmLabel: 'Borrar',
    );
    if (!confirmed || !context.mounted) return;
    await runItemAction(
      context,
      () => ref.read(homebrewActionsProvider).delete(campaign.id, item.id),
      success: 'Objeto borrado.',
      errors: const {409: 'Está en uso: algún objeto de un personaje o una tienda lo usa.'},
    );
  }

  Future<void> _openForm(BuildContext context, {ItemSummary? editing}) => Navigator.of(context)
      .push<bool>(
        MaterialPageRoute(
          builder: (_) => HomebrewFormPage(campaignId: campaign.id, editing: editing),
        ),
      );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = campaign.myRole.isAtLeastDm;
    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: isDm
          ? OfflineAwareFab(
              fabKey: const Key('homebrew-new'),
              onPressed: () => _openForm(context),
              icon: const Icon(Icons.add),
              label: const Text('Nuevo objeto'),
            )
          : null,
      body: Column(
        children: [
          if (!isDm)
            ExpansionTile(
              key: const Key('content-packs-expander'),
              leading: const Icon(Icons.inventory_2_outlined),
              title: const Text('Paquetes de contenido'),
              childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
              children: [CampaignContentPacksSection(campaignId: campaign.id, editable: false)],
            ),
          Expanded(child: _items(context, ref, isDm)),
        ],
      ),
    );
  }

  Widget _items(BuildContext context, WidgetRef ref, bool isDm) => ItemSearchList(
    campaignId: campaign.id,
    initialSource: ItemSource.all,
    showSourceFilter: true,
    onSelected: (item) =>
        ref.read(campaignSystemUiProvider(campaign.id)).openCatalogItem(context, item.id),
    // Only the campaign's own items can be edited; SRD items are read-only.
    trailingBuilder: isDm
        ? (context, item) => !item.isHomebrew
              ? null
              : PopupMenuButton<String>(
                  key: Key('homebrew-menu-${item.id}'),
                  onSelected: (value) => value == 'edit'
                      ? _openForm(context, editing: item)
                      : _delete(context, ref, item),
                  itemBuilder: (_) => const [
                    PopupMenuItem(value: 'edit', child: Text('Editar')),
                    PopupMenuItem(value: 'delete', child: Text('Borrar')),
                  ],
                )
        : null,
  );
}

/// Create or edit a homebrew item with the item form of the game system
/// ([GameSystemUi.itemFormSection]). Pops with true when it was saved.
class HomebrewFormPage extends ConsumerStatefulWidget {
  const HomebrewFormPage({super.key, required this.campaignId, this.editing});

  final String campaignId;

  /// The item to edit; null creates a new one.
  final ItemSummary? editing;

  @override
  ConsumerState<HomebrewFormPage> createState() => _HomebrewFormPageState();
}

class _HomebrewFormPageState extends ConsumerState<HomebrewFormPage> {
  final _formKey = GlobalKey();
  bool _busy = false;

  Future<void> _save() async {
    final form = _formKey.currentState;
    if (form is! ItemFormReader) return;
    final reader = form as ItemFormReader;
    if (!reader.validate()) return;
    final input = reader.readTemplate();
    final editing = widget.editing;
    final actions = ref.read(homebrewActionsProvider);
    setState(() => _busy = true);
    final done = await runItemAction(
      context,
      () async {
        if (editing == null) {
          await actions.create(widget.campaignId, input);
        } else {
          await actions.update(widget.campaignId, editing.id, input);
        }
      },
      success: editing == null ? 'Objeto creado.' : 'Objeto actualizado.',
      errors: const {404: 'El objeto ya no existe o no es de esta campaña.'},
    );
    if (!mounted) return;
    setState(() => _busy = false);
    if (done) Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final editing = widget.editing;
    final system = ref.watch(campaignSystemUiProvider(widget.campaignId));
    Widget body;
    if (editing == null) {
      body = _FormBody(
        form: system.itemFormSection(ItemFormScope(formKey: _formKey, templateMode: true)),
        busy: _busy,
        onSave: _save,
      );
    } else {
      final template = system.itemTemplate(editing.id);
      body = ref
          .watch(template)
          .when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) =>
                ItemsErrorView(error: error, onRetry: () => ref.invalidate(template)),
            data: (detail) => _FormBody(
              form: system.itemFormSection(
                ItemFormScope(formKey: _formKey, template: detail, templateMode: true),
              ),
              busy: _busy,
              onSave: _save,
            ),
          );
    }
    return Scaffold(
      appBar: AppBar(title: Text(editing == null ? 'Nuevo objeto' : 'Editar objeto')),
      body: body,
    );
  }
}

class _FormBody extends StatelessWidget {
  const _FormBody({required this.form, required this.busy, required this.onSave});

  /// The item form of the game system.
  final Widget? form;
  final bool busy;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: EdgeInsets.fromLTRB(16, 12, 16, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        ?form,
        const SizedBox(height: 8),
        FilledButton.icon(
          key: const Key('homebrew-save'),
          onPressed: busy ? null : onSave,
          icon: const Icon(Icons.check),
          label: const Text('Guardar'),
        ),
      ],
    );
  }
}
