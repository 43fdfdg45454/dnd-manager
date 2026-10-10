import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';

/// Maps an error to a Spanish message, with optional per-status overrides.
typedef ErrorDescriber = String Function(Object error, {Map<int, String> byStatus});

/// Runs [action] and reports [success] or a Spanish error message in a
/// SnackBar ([describe] defaults to the campaign texts). Returns true when the
/// action completed.
Future<bool> runAction(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
  ErrorDescriber describe = describeCampaignError,
}) async {
  // Resolved up front: the context may be gone once the request finishes.
  final messenger = ScaffoldMessenger.of(context);
  void show(String message) => messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
  try {
    await action();
    if (success != null) show(success);
    return true;
  } catch (error) {
    show(describe(error, byStatus: errors));
    return false;
  }
}
