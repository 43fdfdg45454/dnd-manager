import 'package:dio/dio.dart' show DioException;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/data/campaigns_repository.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../../characters/data/characters_controller.dart';
import '../../characters/data/models.dart';
import '../../characters/domain/payload_format.dart';

const _filters = <(String, ChangeRequestStatus?)>[
  ('Pendientes', ChangeRequestStatus.pending),
  ('Aprobadas', ChangeRequestStatus.approved),
  ('Rechazadas', ChangeRequestStatus.rejected),
  ('Canceladas', ChangeRequestStatus.cancelled),
  ('Todas', null),
];

final _dateFormat = DateFormat('dd/MM/yyyy HH:mm');

/// Change requests of a campaign. DMs see every request and can approve or
/// reject it; players see their own and can cancel the pending ones.
class ChangeRequestsPage extends ConsumerStatefulWidget {
  const ChangeRequestsPage({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<ChangeRequestsPage> createState() => _ChangeRequestsPageState();
}

class _ChangeRequestsPageState extends ConsumerState<ChangeRequestsPage> {
  ChangeRequestStatus? _status = ChangeRequestStatus.pending;

  ChangeRequestsKey get _key => (campaignId: widget.campaignId, status: _status);

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final isDm = ref
        .watch(campaignDetailControllerProvider(widget.campaignId))
        .maybeWhen(data: (c) => c.myRole.isAtLeastDm, orElse: () => false);
    final requests = ref.watch(changeRequestsControllerProvider(_key));

    return Scaffold(
      appBar: AppBar(title: const Text('Solicitudes de cambio')),
      body: OfflineBannerLayout(
        scopes: [
          staleTree('${CampaignsRepository.campaignPath(widget.campaignId)}/change-requests'),
        ],
        child: Column(
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  for (final (label, status) in _filters)
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        key: Key('filter-${status?.apiValue ?? 'all'}'),
                        label: Text(label),
                        selected: _status == status,
                        onSelected: (_) => setState(() => _status = status),
                      ),
                    ),
                ],
              ),
            ),
            Expanded(
              child: requests.when(
                skipLoadingOnReload: true,
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(describeCharacterError(error), textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        FilledButton.icon(
                          onPressed: () => ref.invalidate(changeRequestsControllerProvider(_key)),
                          icon: const Icon(Icons.refresh),
                          label: const Text('Reintentar'),
                        ),
                      ],
                    ),
                  ),
                ),
                data: (list) => RefreshIndicator(
                  onRefresh: () async => ref.invalidate(changeRequestsControllerProvider(_key)),
                  child: list.isEmpty
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          children: const [
                            SizedBox(height: 96),
                            Center(child: Text('No hay solicitudes en esta lista')),
                          ],
                        )
                      : ListView(
                          padding: const EdgeInsets.only(bottom: 24),
                          children: [
                            for (final request in list)
                              _RequestCard(
                                key: Key('change-request-${request.id}'),
                                request: request,
                                listKey: _key,
                                isDm: isDm,
                                myUserId: myUserId,
                              ),
                          ],
                        ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RequestCard extends ConsumerWidget {
  const _RequestCard({
    super.key,
    required this.request,
    required this.listKey,
    required this.isDm,
    required this.myUserId,
  });

  final ChangeRequest request;
  final ChangeRequestsKey listKey;
  final bool isDm;
  final String myUserId;

  ChangeRequestsController _controller(WidgetRef ref) =>
      ref.read(changeRequestsControllerProvider(listKey).notifier);

  /// A 409 means somebody else resolved it first: refresh the list as well.
  Future<void> Function() _guard(WidgetRef ref, Future<void> Function() action) => () async {
    try {
      await action();
    } on DioException catch (error) {
      if (error.response?.statusCode == 409) {
        ref.invalidate(changeRequestsControllerProvider);
      }
      rethrow;
    }
  };

  Future<void> _approve(BuildContext context, WidgetRef ref) async {
    final comment = await showDialog<String>(
      context: context,
      builder: (_) => const _CommentDialog(
        title: 'Aprobar solicitud',
        label: 'Comentario (opcional)',
        confirmLabel: 'Aprobar',
        required: false,
      ),
    );
    if (comment == null || !context.mounted) return;
    await runAction(
      context,
      _guard(
        ref,
        () => _controller(ref).approve(request.id, comment: comment.isEmpty ? null : comment),
      ),
      success: 'Solicitud aprobada.',
      describe: describeCharacterError,
    );
  }

  Future<void> _reject(BuildContext context, WidgetRef ref) async {
    final comment = await showDialog<String>(
      context: context,
      builder: (_) => const _CommentDialog(
        title: 'Rechazar solicitud',
        label: 'Motivo del rechazo',
        confirmLabel: 'Rechazar',
        required: true,
      ),
    );
    if (comment == null || !context.mounted) return;
    await runAction(
      context,
      _guard(ref, () => _controller(ref).reject(request.id, comment: comment)),
      success: 'Solicitud rechazada.',
      describe: describeCharacterError,
    );
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Cancelar solicitud',
      message: '¿Seguro que quieres cancelar esta solicitud?',
      confirmLabel: 'Cancelar solicitud',
    );
    if (!confirmed || !context.mounted) return;
    await runAction(
      context,
      _guard(ref, () => _controller(ref).cancel(request.id)),
      success: 'Solicitud cancelada.',
      describe: describeCharacterError,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final r = request;
    final lines = describePayload(r.payload);
    final canResolve = r.isPending && isDm;
    final canCancel = r.isPending && r.requestedByUserId == myUserId;

    return Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ExpansionTile(
            key: Key('change-request-tile-${r.id}'),
            title: Text(r.characterName.isEmpty ? 'Personaje' : r.characterName),
            subtitle: Text(
              '${r.type.label} · ${r.status.label}\n'
              'Solicitada por ${r.requestedByDisplayName}'
              '${r.createdAt == null ? '' : ' · ${_dateFormat.format(r.createdAt!.toLocal())}'}',
            ),
            expandedCrossAxisAlignment: CrossAxisAlignment.start,
            childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            children: [
              if (lines.isEmpty)
                const Text('Esta solicitud no incluye cambios detallados.')
              else
                for (final line in lines)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 3),
                    child: Text.rich(
                      TextSpan(
                        children: [
                          TextSpan(
                            text: '${line.label}: ',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          TextSpan(text: line.value),
                        ],
                      ),
                      style: theme.textTheme.bodyMedium,
                    ),
                  ),
              if (r.resolvedByDisplayName != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Resuelta por ${r.resolvedByDisplayName}'
                  '${r.resolvedAt == null ? '' : ' · ${_dateFormat.format(r.resolvedAt!.toLocal())}'}',
                  style: theme.textTheme.bodySmall,
                ),
              ],
              if (r.comment != null) ...[
                const SizedBox(height: 4),
                Text('Comentario: ${r.comment}', style: theme.textTheme.bodyMedium),
              ],
            ],
          ),
          if (canResolve || canCancel)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (canResolve) ...[
                    FilledButton(
                      key: Key('approve-${r.id}'),
                      onPressed: () => _approve(context, ref),
                      child: const Text('Aprobar'),
                    ),
                    OutlinedButton(
                      key: Key('reject-${r.id}'),
                      onPressed: () => _reject(context, ref),
                      child: const Text('Rechazar'),
                    ),
                  ],
                  if (canCancel)
                    OutlinedButton(
                      key: Key('cancel-${r.id}'),
                      onPressed: () => _cancel(context, ref),
                      child: const Text('Cancelar solicitud'),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Asks for a comment. Pops the trimmed text (possibly empty when optional) or
/// null if cancelled.
class _CommentDialog extends StatefulWidget {
  const _CommentDialog({
    required this.title,
    required this.label,
    required this.confirmLabel,
    required this.required,
  });

  final String title;
  final String label;
  final String confirmLabel;
  final bool required;

  @override
  State<_CommentDialog> createState() => _CommentDialogState();
}

class _CommentDialogState extends State<_CommentDialog> {
  final _formKey = GlobalKey<FormState>();
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop<String>(_controller.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: Form(
        key: _formKey,
        child: TextFormField(
          key: const Key('comment-field'),
          controller: _controller,
          maxLength: 500,
          minLines: 2,
          maxLines: 5,
          textCapitalization: TextCapitalization.sentences,
          decoration: InputDecoration(labelText: widget.label),
          validator: (value) => widget.required && (value == null || value.trim().isEmpty)
              ? 'Escribe un comentario'
              : null,
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Volver')),
        FilledButton(
          key: const Key('comment-submit'),
          onPressed: _submit,
          child: Text(widget.confirmLabel),
        ),
      ],
    );
  }
}
