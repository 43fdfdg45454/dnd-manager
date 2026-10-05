import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../characters/data/characters_controller.dart';
import '../../characters/ui/characters_tab.dart';
import '../../items/ui/homebrew_tab.dart';
import '../../items/ui/shops_tab.dart';
import '../data/campaigns_controller.dart';
import '../domain/campaign_models.dart';
import 'add_member_dialog.dart';
import 'campaign_form_dialog.dart';
import 'confirm_dialog.dart';
import 'feedback.dart';
import 'transfer_ownership_dialog.dart';

/// Campaign detail with the "Resumen", "Miembros", "Personajes", "Tiendas" and
/// "Objetos" tabs.
class CampaignDetailPage extends ConsumerWidget {
  const CampaignDetailPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(campaignDetailControllerProvider(campaignId));

    return DefaultTabController(
      length: 5,
      child: Scaffold(
        appBar: AppBar(
          title: Text(detail.value?.name ?? 'Campaña'),
          actions: [
            if (detail.value != null) ...[
              IconButton(
                key: const Key('transactions-button'),
                tooltip: 'Transacciones',
                onPressed: () => context.push(AppRoutes.transactions(campaignId)),
                icon: const Icon(Icons.receipt_long_outlined),
              ),
              _ChangeRequestsButton(campaign: detail.value!),
            ],
          ],
          bottom: const TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              Tab(key: Key('tab-summary'), text: 'Resumen'),
              Tab(key: Key('tab-members'), text: 'Miembros'),
              Tab(key: Key('tab-characters'), text: 'Personajes'),
              Tab(key: Key('tab-shops'), text: 'Tiendas'),
              Tab(key: Key('tab-objects'), text: 'Objetos'),
            ],
          ),
        ),
        body: detail.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(describeCampaignError(error), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: () => ref.invalidate(campaignDetailControllerProvider(campaignId)),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ),
          data: (campaign) => TabBarView(
            children: [
              _SummaryTab(campaign: campaign),
              _MembersTab(campaign: campaign),
              CharactersTab(campaign: campaign),
              ShopsTab(campaign: campaign),
              HomebrewTab(campaign: campaign),
            ],
          ),
        ),
      ),
    );
  }
}

/// App bar shortcut to the change requests; DMs see the pending count as a badge.
class _ChangeRequestsButton extends ConsumerWidget {
  const _ChangeRequestsButton({required this.campaign});

  final CampaignDetail campaign;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDm = campaign.myRole.isAtLeastDm;
    final pending = isDm ? ref.watch(pendingChangeRequestCountProvider(campaign.id)) : 0;
    return IconButton(
      key: const Key('change-requests-button'),
      tooltip: 'Solicitudes de cambio',
      onPressed: () => context.push(AppRoutes.changeRequests(campaign.id)),
      icon: Badge(
        key: const Key('change-requests-badge'),
        isLabelVisible: pending > 0,
        label: Text('$pending'),
        child: const Icon(Icons.fact_check_outlined),
      ),
    );
  }
}

CampaignDetailController _controllerOf(WidgetRef ref, String id) =>
    ref.read(campaignDetailControllerProvider(id).notifier);

class _SummaryTab extends ConsumerWidget {
  const _SummaryTab({required this.campaign});

  final CampaignDetail campaign;

  Future<void> _edit(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<CampaignFormData>(
      context: context,
      builder: (_) => CampaignFormDialog(
        title: 'Editar campaña',
        submitLabel: 'Guardar',
        initialName: campaign.name,
        initialDescription: campaign.description,
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => _controllerOf(ref, campaign.id).edit(name: data.name, description: data.description),
      success: 'Campaña actualizada.',
      errors: const {400: 'Datos no válidos. Revisa el nombre y la descripción.'},
    );
  }

  Future<void> _transfer(BuildContext context, WidgetRef ref, String myUserId) async {
    final data = await showDialog<TransferData>(
      context: context,
      builder: (_) => TransferOwnershipDialog(
        candidates: campaign.members.where((m) => m.userId != myUserId).toList(),
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () =>
          _controllerOf(ref, campaign.id).transferOwnership(data.to.userId, data.previousOwnerRole),
      success: 'Propiedad transferida a ${data.to.displayName}.',
      errors: const {400: 'El destino debe ser otro miembro de la campaña.'},
    );
  }

  Future<void> _leave(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Salir de la campaña',
      message: '¿Seguro que quieres salir de "${campaign.name}"? Perderás el acceso a ella.',
      confirmLabel: 'Salir',
    );
    if (!confirmed || !context.mounted) return;
    final done = await runAction(
      context,
      () => _controllerOf(ref, campaign.id).leave(),
      success: 'Has salido de la campaña.',
      errors: const {400: 'El dueño debe transferir la propiedad antes de salir.'},
    );
    if (done && context.mounted) context.go(AppRoutes.home);
  }

  Future<void> _delete(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar campaña',
      message:
          '¿Seguro que quieres eliminar "${campaign.name}"? Se borrarán todos sus datos y no se puede deshacer.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !context.mounted) return;
    final done = await runAction(
      context,
      () => _controllerOf(ref, campaign.id).delete(),
      success: 'Campaña eliminada.',
    );
    if (done && context.mounted) context.go(AppRoutes.home);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final role = campaign.myRole;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(campaign.name, style: theme.textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(
          'Dueño: ${campaign.ownerDisplayName} · Tu rol: ${role.label}',
          key: const Key('campaign-meta'),
          style: theme.textTheme.bodyMedium,
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: SizedBox(
              width: double.infinity,
              child: campaign.description.trim().isEmpty
                  ? Text(
                      'Esta campaña no tiene descripción.',
                      style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
                    )
                  : SelectableText(
                      campaign.description,
                      key: const Key('campaign-description-text'),
                    ),
            ),
          ),
        ),
        const SizedBox(height: 16),
        if (role.isAtLeastDm)
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              key: const Key('campaign-edit'),
              onPressed: () => _edit(context, ref),
              icon: const Icon(Icons.edit_outlined),
              label: const Text('Editar'),
            ),
          ),
        if (role.isOwner) ...[
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              key: const Key('campaign-transfer'),
              onPressed: () => _transfer(context, ref, myUserId),
              icon: const Icon(Icons.swap_horiz),
              label: const Text('Transferir propiedad'),
            ),
          ),
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              key: const Key('campaign-delete'),
              style: OutlinedButton.styleFrom(foregroundColor: theme.colorScheme.error),
              onPressed: () => _delete(context, ref),
              icon: const Icon(Icons.delete_outline),
              label: const Text('Eliminar campaña'),
            ),
          ),
        ] else ...[
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton.icon(
              key: const Key('campaign-leave'),
              onPressed: () => _leave(context, ref),
              icon: const Icon(Icons.logout),
              label: const Text('Salir de la campaña'),
            ),
          ),
        ],
      ],
    );
  }
}

enum _MemberAction { changeRole, remove }

class _MembersTab extends ConsumerWidget {
  const _MembersTab({required this.campaign});

  final CampaignDetail campaign;

  Future<void> _add(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<NewMemberData>(
      context: context,
      builder: (_) => AddMemberDialog(
        canAddDm: campaign.myRole.isOwner,
        excludedUserIds: {for (final m in campaign.members) m.userId},
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => _controllerOf(ref, campaign.id).addMember(data.user.id, data.role),
      success: '${data.user.displayName} se ha añadido como ${data.role.label}.',
    );
  }

  Future<void> _changeRole(BuildContext context, WidgetRef ref, Member member) async {
    final role = await showDialog<CampaignRole>(
      context: context,
      builder: (dialogContext) => SimpleDialog(
        title: Text('Rol de ${member.displayName}'),
        children: [
          for (final r in const [CampaignRole.dm, CampaignRole.player])
            SimpleDialogOption(
              onPressed: () => Navigator.of(dialogContext).pop(r),
              child: Row(
                children: [
                  Icon(r == member.role ? Icons.radio_button_checked : Icons.radio_button_off),
                  const SizedBox(width: 12),
                  Text(r.label),
                ],
              ),
            ),
        ],
      ),
    );
    if (role == null || role == member.role || !context.mounted) return;
    await runAction(
      context,
      () => _controllerOf(ref, campaign.id).changeRole(member.userId, role),
      success: 'Rol actualizado.',
    );
  }

  Future<void> _remove(BuildContext context, WidgetRef ref, Member member) => runAction(
    context,
    () => _controllerOf(ref, campaign.id).removeMember(member.userId),
    success: '${member.displayName} ya no es miembro de la campaña.',
    errors: const {400: 'No se puede quitar al dueño de la campaña.'},
  );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final myRole = campaign.myRole;

    return ListView(
      padding: const EdgeInsets.only(bottom: 24),
      children: [
        if (myRole.isAtLeastDm)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.tonalIcon(
                key: const Key('members-add'),
                onPressed: () => _add(context, ref),
                icon: const Icon(Icons.person_add_alt_1),
                label: const Text('Añadir'),
              ),
            ),
          ),
        for (final member in campaign.members)
          _MemberTile(
            member: member,
            isSelf: member.userId == myUserId,
            canChangeRole: MemberPermissions.canChangeRole(myRole, member),
            canRemove: MemberPermissions.canRemove(myRole, myUserId, member),
            onAction: (action) => switch (action) {
              _MemberAction.changeRole => _changeRole(context, ref, member),
              _MemberAction.remove => _remove(context, ref, member),
            },
          ),
      ],
    );
  }
}

class _MemberTile extends StatelessWidget {
  const _MemberTile({
    required this.member,
    required this.isSelf,
    required this.canChangeRole,
    required this.canRemove,
    required this.onAction,
  });

  final Member member;
  final bool isSelf;
  final bool canChangeRole;
  final bool canRemove;
  final ValueChanged<_MemberAction> onAction;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      key: Key('member-${member.userId}'),
      leading: CircleAvatar(
        child: Text(member.displayName.isEmpty ? '?' : member.displayName[0].toUpperCase()),
      ),
      title: Text(isSelf ? '${member.displayName} (tú)' : member.displayName),
      subtitle: Text(member.email),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Chip(
            label: Text(member.role.label),
            visualDensity: VisualDensity.compact,
            padding: EdgeInsets.zero,
          ),
          if (canChangeRole || canRemove)
            PopupMenuButton<_MemberAction>(
              key: Key('member-menu-${member.userId}'),
              onSelected: onAction,
              itemBuilder: (_) => [
                if (canChangeRole)
                  const PopupMenuItem(value: _MemberAction.changeRole, child: Text('Cambiar rol')),
                if (canRemove)
                  const PopupMenuItem(value: _MemberAction.remove, child: Text('Quitar')),
              ],
            ),
        ],
      ),
    );
  }
}
