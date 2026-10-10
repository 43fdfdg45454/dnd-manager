import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';
import '../../campaigns/ui/feedback.dart';
import '../data/models.dart';

/// Shown when a write became a change request instead of being applied.
const sentToDmMessage = 'Enviado al DM para aprobación';

/// Runs an inventory write and reports [applied] (200/201/204) or
/// [sentToDmMessage] (202) in a SnackBar, or a Spanish error. Returns true
/// when the request went through.
Future<bool> runWrite(
  BuildContext context,
  Future<InventoryWriteResult> Function() action, {
  required String applied,
  Map<int, String> errors = const {},
}) async {
  final messenger = ScaffoldMessenger.of(context);
  InventoryWriteResult? result;
  final done = await runAction(
    context,
    () async => result = await action(),
    errors: errors,
    describe: describeItemError,
  );
  if (!done) return false;
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(result is InventoryPending ? sentToDmMessage : applied)));
  return true;
}

/// [runAction] with the item error texts.
Future<bool> runItemAction(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
}) => runAction(context, action, success: success, errors: errors, describe: describeItemError);

/// Standard error view with a retry button.
class ItemsErrorView extends StatelessWidget {
  const ItemsErrorView({super.key, required this.error, required this.onRetry, this.byStatus});

  final Object error;
  final VoidCallback onRetry;
  final Map<int, String>? byStatus;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              describeItemError(error, byStatus: byStatus ?? const {}),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}
