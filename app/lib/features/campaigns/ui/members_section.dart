import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/ui/offline_widgets.dart';
import '../data/campaigns_controller.dart';
import '../domain/campaign_models.dart';
import 'add_member_dialog.dart';
import 'feedback.dart';
import 'member_error.dart';

CampaignDetailController _controllerOf(WidgetRef ref, String id) =>
    ref.read(campaignDetailControllerProvider(id).notifier);

enum _MemberAction { changeRole, remove }

/// "Miembros" section of a campaign: the members with their role, plus adding,
/// changing roles and removing as [MemberPermissions] allow.
class MembersSection extends ConsumerWidget {
  const MembersSection({super.key, required this.campaign});

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

  /// The dialog stays open while the role changes, so an error (such as the
  /// 409 of a player who still owns characters) is shown inline.
  Future<void> _changeRole(BuildContext context, WidgetRef ref, Member member) async {
    final messenger = ScaffoldMessenger.of(context);
    final role = await showDialog<CampaignRole>(
      context: context,
      builder: (_) => _RoleDialog(
        member: member,
        onSubmit: (role) => _controllerOf(ref, campaign.id).changeRole(member.userId, role),
      ),
    );
    if (role == null) return;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Rol actualizado.')));
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
              child: OfflineAware(
                builder: (context, canWrite) => FilledButton.tonalIcon(
                  key: const Key('members-add'),
                  onPressed: !canWrite ? null : () => _add(context, ref),
                  icon: const Icon(Icons.person_add_alt_1),
                  label: const Text('Añadir'),
                ),
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

/// Role choice of a member (DM or Player). Choosing a different role runs
/// [onSubmit]; the dialog pops with the role once applied or shows the error.
class _RoleDialog extends StatefulWidget {
  const _RoleDialog({required this.member, required this.onSubmit});

  final Member member;
  final Future<void> Function(CampaignRole role) onSubmit;

  @override
  State<_RoleDialog> createState() => _RoleDialogState();
}

class _RoleDialogState extends State<_RoleDialog> {
  bool _busy = false;
  String? _error;

  Future<void> _choose(CampaignRole role) async {
    if (role == widget.member.role) {
      Navigator.of(context).pop();
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onSubmit(role);
      if (mounted) Navigator.of(context).pop(role);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = describeMemberError(error);
      });
    }
  }

  @override
  Widget build(BuildContext context) => SimpleDialog(
    title: Text('Rol de ${widget.member.displayName}'),
    children: [
      for (final r in const [CampaignRole.dm, CampaignRole.player])
        SimpleDialogOption(
          key: Key('member-role-${r.apiValue}'),
          onPressed: _busy ? null : () => _choose(r),
          child: Row(
            children: [
              Icon(r == widget.member.role ? Icons.radio_button_checked : Icons.radio_button_off),
              const SizedBox(width: 12),
              Text(r.label),
            ],
          ),
        ),
      if (_error != null)
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 0),
          child: InlineMemberError(key: const Key('member-role-error'), message: _error!),
        ),
    ],
  );
}
