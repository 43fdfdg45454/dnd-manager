import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';
import '../../../../core/auth/auth_state.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../sessions/domain/sessions_format.dart';
import '../../../sessions/ui/calendar_settings_dialog.dart';
import '../../../sessions/ui/session_widgets.dart';
import '../../data/campaigns_controller.dart';
import '../../domain/campaign_models.dart';
import '../campaign_form_dialog.dart';
import '../confirm_dialog.dart';
import '../feedback.dart';
import '../transfer_ownership_dialog.dart';

CampaignDetailController _controllerOf(WidgetRef ref, String id) =>
    ref.read(campaignDetailControllerProvider(id).notifier);

/// "Ajustes" section of a campaign: description, calendar settings and the
/// edit, transfer, delete and leave actions allowed to the user's role.
class CampaignSettingsSection extends ConsumerWidget {
  const CampaignSettingsSection({super.key, required this.campaign});

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

  Future<void> _editCalendarSettings(BuildContext context, WidgetRef ref) async {
    final data = await showDialog<CalendarSettings>(
      context: context,
      builder: (_) => CalendarSettingsDialog(
        initialTimeZoneId: campaign.timeZoneId,
        initialOffsets: campaign.reminderOffsetsMinutes,
      ),
    );
    if (data == null || !context.mounted) return;
    await runAction(
      context,
      () => _controllerOf(ref, campaign.id).updateSettings(
        timeZoneId: data.timeZoneId,
        reminderOffsetsMinutes: data.reminderOffsetsMinutes,
      ),
      success: 'Ajustes del calendario guardados.',
      errors: const {400: 'Zona horaria o recordatorios no válidos.'},
      describe: describeSessionError,
    );
  }

  Future<void> _transfer(BuildContext context, WidgetRef ref, String myUserId) async {
    final messenger = ScaffoldMessenger.of(context);
    final data = await showDialog<TransferData>(
      context: context,
      builder: (_) => TransferOwnershipDialog(
        candidates: campaign.members.where((m) => m.userId != myUserId).toList(),
        onSubmit: (data) => _controllerOf(
          ref,
          campaign.id,
        ).transferOwnership(data.to.userId, data.previousOwnerRole),
      ),
    );
    if (data == null) return;
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text('Propiedad transferida a ${data.to.displayName}.')));
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
        Card(
          child: ListTile(
            key: const Key('campaign-calendar-info'),
            leading: const Icon(Icons.event_outlined),
            title: Text('Zona horaria: ${campaign.timeZoneId}'),
            subtitle: Text(
              campaign.reminderOffsetsMinutes.isEmpty
                  ? 'Sin recordatorios por correo.'
                  : 'Recordatorios: ${campaign.reminderOffsetsMinutes.map(formatOffsetBefore).join(', ')}.',
            ),
            trailing: role.isAtLeastDm
                ? IconButton(
                    key: const Key('campaign-calendar-settings'),
                    tooltip: 'Ajustes del calendario',
                    onPressed: () => _editCalendarSettings(context, ref),
                    icon: const Icon(Icons.tune),
                  )
                : null,
          ),
        ),
        const SizedBox(height: 16),
        Align(
          alignment: Alignment.centerLeft,
          child: OutlinedButton.icon(
            key: const Key('campaign-documents'),
            onPressed: () => context.push(AppRoutes.campaignDocuments(campaign.id)),
            icon: const Icon(Icons.picture_as_pdf_outlined),
            label: const Text('Documentos recomendados'),
          ),
        ),
        const SizedBox(height: 8),
        if (role.isAtLeastDm)
          Align(
            alignment: Alignment.centerLeft,
            child: OfflineAware(
              builder: (context, canWrite) => OutlinedButton.icon(
                key: const Key('campaign-edit'),
                onPressed: !canWrite ? null : () => _edit(context, ref),
                icon: const Icon(Icons.edit_outlined),
                label: const Text('Editar'),
              ),
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
