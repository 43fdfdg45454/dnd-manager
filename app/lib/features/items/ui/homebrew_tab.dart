import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../campaigns/domain/campaign_models.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/models.dart' show ItemSummary;
import '../data/campaign_items_repository.dart';
import '../data/items_controllers.dart';
import '../domain/item_form_data.dart';
import 'item_feedback.dart';
import 'item_fields_form.dart';
import 'item_search_list.dart';

/// "Objetos" tab of a campaign: the homebrew items with search. At least a DM
/// creates, edits and deletes them.
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
          ? FloatingActionButton.extended(
              key: const Key('homebrew-new'),
              onPressed: () => _openForm(context),
              icon: const Icon(Icons.add),
              label: const Text('Nuevo objeto'),
            )
          : null,
      body: ItemSearchList(
        campaignId: campaign.id,
        initialSource: ItemSource.homebrew,
        showSourceFilter: false,
        onSelected: (item) => context.push(AppRoutes.item(item.id)),
        trailingBuilder: isDm
            ? (context, item) => PopupMenuButton<String>(
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
      ),
    );
  }
}

/// Create or edit a homebrew item with the advanced form. Pops with true when
/// it was saved.
class HomebrewFormPage extends ConsumerStatefulWidget {
  const HomebrewFormPage({super.key, required this.campaignId, this.editing});

  final String campaignId;

  /// The item to edit; null creates a new one.
  final ItemSummary? editing;

  @override
  ConsumerState<HomebrewFormPage> createState() => _HomebrewFormPageState();
}

class _HomebrewFormPageState extends ConsumerState<HomebrewFormPage> {
  final _formKey = GlobalKey<ItemFieldsFormState>();
  bool _busy = false;

  Future<void> _save() async {
    final form = _formKey.currentState;
    if (form == null || !form.validate()) return;
    final input = form.read().toInput();
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
    Widget body;
    if (editing == null) {
      body = _FormBody(
        formKey: _formKey,
        initial: const ItemFormData(),
        busy: _busy,
        onSave: _save,
      );
    } else {
      body = ref
          .watch(itemDetailProvider(editing.id))
          .when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => ItemsErrorView(
              error: error,
              onRetry: () => ref.invalidate(itemDetailProvider(editing.id)),
            ),
            data: (detail) => _FormBody(
              formKey: _formKey,
              initial: ItemFormData.fromDetail(detail),
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
  const _FormBody({
    required this.formKey,
    required this.initial,
    required this.busy,
    required this.onSave,
  });

  final GlobalKey<ItemFieldsFormState> formKey;
  final ItemFormData initial;
  final bool busy;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: EdgeInsets.fromLTRB(16, 12, 16, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        ItemFieldsForm(key: formKey, initial: initial, templateMode: true),
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
