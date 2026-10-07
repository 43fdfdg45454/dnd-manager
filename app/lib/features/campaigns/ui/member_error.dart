import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../../../core/network/api_error.dart';

/// Error of a membership change shown inside its dialog. A 409 carries the
/// server's explanation (for example, a player who still owns characters cannot
/// become DM or owner), so its `detail` is shown as is.
String describeMemberError(Object error, {Map<int, String> byStatus = const {}}) {
  if (error is DioException && error.response?.statusCode == 409) {
    final detail = problemDetail(error);
    if (detail != null) return detail;
  }
  return describeCampaignError(error, byStatus: byStatus);
}

/// Inline error text of the membership dialogs.
class InlineMemberError extends StatelessWidget {
  const InlineMemberError({super.key, required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.error_outline, size: 20, color: scheme.error),
        const SizedBox(width: 8),
        Expanded(
          child: Text(message, style: TextStyle(color: scheme.error)),
        ),
      ],
    );
  }
}
