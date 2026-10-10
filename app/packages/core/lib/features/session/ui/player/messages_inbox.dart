import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/motion/wax_seal.dart';
import '../../../../core/theme/app_icon.dart';
import '../../../../core/theme/components.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/theme/tokens.dart';
import '../../../../core/ui/markdown_view.dart';
import '../../data/models.dart';
import '../../data/session_controllers.dart';
import '../session_feedback.dart';

/// "Mensajes del DM": the secret messages received in the campaign, newest
/// first, with the unread count. Opening one shows it and marks it as read;
/// an unread one opens by breaking its wax seal ([SealBreak]).
class MessagesInbox extends ConsumerWidget {
  const MessagesInbox({super.key, required this.campaignId});

  final String campaignId;

  Future<void> _open(BuildContext context, WidgetRef ref, DirectMessage message) async {
    final sealed = !message.isRead;
    if (sealed) {
      // Marked as read on opening; a failure only leaves it unread.
      runTableAction(
        context,
        () => ref.read(messagesControllerProvider(campaignId).notifier).markRead(message.id),
      );
    }
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Para ${message.characterName}'),
        content: SizedBox(
          width: double.maxFinite,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (sealed) const Center(child: _BreakingSeal()),
                Text(_from(message), style: Theme.of(dialogContext).textTheme.bodySmall),
                const SizedBox(height: 8),
                MarkdownView(
                  key: const Key('message-body-view'),
                  data: message.body,
                  selectable: true,
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            key: const Key('message-close'),
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cerrar'),
          ),
        ],
      ),
    );
  }

  static String _from(DirectMessage m) {
    final date = m.sentAt == null
        ? null
        : DateFormat('dd/MM/yyyy HH:mm').format(m.sentAt!.toLocal());
    return [if (m.senderDisplayName.isNotEmpty) 'De ${m.senderDisplayName}', ?date].join(' · ');
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final tokens = context.tokens;
    final messages = ref.watch(messagesControllerProvider(campaignId));
    final unread =
        ref.watch(unreadMessagesCountProvider(campaignId)).value ??
        (messages.value?.where((m) => !m.isRead).length ?? 0);

    return ParchmentCard(
      key: const Key('messages-inbox'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Badge(
                key: const Key('messages-unread-badge'),
                isLabelVisible: unread > 0,
                label: Text('$unread'),
                child: AppIcon(AppIcons.envelope, color: tokens.gold),
              ),
              const SizedBox(width: 12),
              Expanded(child: Text('Mensajes del DM', style: theme.textTheme.titleMedium)),
            ],
          ),
          messages.when(
            skipLoadingOnReload: true,
            loading: () => const Padding(
              padding: EdgeInsets.all(12),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => TextButton(
              onPressed: () => ref.invalidate(messagesControllerProvider(campaignId)),
              child: Text('${describeTableError(error)} Reintentar'),
            ),
            data: (list) => list.isEmpty
                ? const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('El DM aún no te ha enviado mensajes.', key: Key('messages-empty')),
                  )
                : Column(
                    children: [
                      for (final m in list)
                        ListTile(
                          key: Key('message-${m.id}'),
                          contentPadding: EdgeInsets.zero,
                          leading: m.isRead
                              ? AppIcon(AppIcons.envelope, color: tokens.inkMuted)
                              : AppIcon(AppIcons.seal, color: tokens.blood),
                          title: Text(
                            m.body.split('\n').first,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: m.isRead ? null : const TextStyle(fontWeight: FontWeight.bold),
                          ),
                          subtitle: Text('Para ${m.characterName} · ${_from(m)}'),
                          onTap: () => _open(context, ref, m),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

/// The wax seal of an unread message: whole when the dialog opens, it breaks
/// right after the first frame (at once under reduced motion).
class _BreakingSeal extends StatefulWidget {
  const _BreakingSeal();

  @override
  State<_BreakingSeal> createState() => _BreakingSealState();
}

class _BreakingSealState extends State<_BreakingSeal> {
  bool _broken = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) setState(() => _broken = true);
    });
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: SealBreak(key: const Key('message-seal'), broken: _broken, size: 48),
  );
}
