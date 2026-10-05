import 'package:flutter/material.dart';

import '../data/models.dart' hide Page;
import 'detail_widgets.dart';

/// Shows a condition's rules in a bottom sheet.
Future<void> showConditionSheet(BuildContext context, Condition condition) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
    builder: (_) => SafeArea(
      child: SingleChildScrollView(
        key: const Key('condition-sheet'),
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(condition.name, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            condition.description.isEmpty
                ? const Text('Sin descripción.')
                : Paragraphs(condition.description),
          ],
        ),
      ),
    ),
  );
}
