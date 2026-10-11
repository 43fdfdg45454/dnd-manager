import 'package:flutter/material.dart';

import '../data/models.dart' hide Page;
import 'detail_widgets.dart';

/// Shows a condition's rules in a bottom sheet.
Future<void> showConditionSheet(BuildContext context, Condition condition) => showInfoSheet(
  context,
  key: const Key('condition-sheet'),
  title: condition.name,
  description: condition.description,
);

/// Bottom sheet with a [title], an optional [subtitle] and the paragraphs of
/// [description] ("Sin descripción." when empty): the quick detail of a
/// skill, a language or a condition.
Future<void> showInfoSheet(
  BuildContext context, {
  required String title,
  String? subtitle,
  List<String> description = const [],
  Key key = const Key('info-sheet'),
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
    builder: (context) => SafeArea(
      child: SingleChildScrollView(
        key: key,
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            if (subtitle != null)
              Text(
                subtitle,
                style: Theme.of(context).textTheme.bodySmall
                    ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
            const SizedBox(height: 12),
            description.isEmpty ? const Text('Sin descripción.') : Paragraphs(description),
          ],
        ),
      ),
    ),
  );
}
