import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';

/// Runs [action] and reports [success] or a Spanish campaign error message in a
/// SnackBar. Returns true when the action completed.
Future<bool> runAction(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
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
    show(describeCampaignError(error, byStatus: errors));
    return false;
  }
}
