import 'package:flutter/material.dart';

import 'detail_widgets.dart';

/// Shows a catalog entry (a tool, a language...) in a bottom sheet: [title],
/// an optional [subtitle] and the paragraphs of [description]; an empty
/// description says so.
Future<void> showReferenceSheet(
  BuildContext context, {
  required String title,
  String? subtitle,
  List<String> description = const [],
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
    builder: (sheetContext) {
      final theme = Theme.of(sheetContext);
      final paragraphs = [
        for (final p in description)
          if (p.trim().isNotEmpty) p,
      ];
      return SafeArea(
        child: SingleChildScrollView(
          key: const Key('reference-sheet'),
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                key: const Key('reference-sheet-title'),
                style: theme.textTheme.titleLarge,
              ),
              if (subtitle != null) ...[
                const SizedBox(height: 4),
                Text(subtitle, style: theme.textTheme.bodySmall),
              ],
              const SizedBox(height: 12),
              if (paragraphs.isEmpty)
                const Text('Sin descripción en el catálogo.', key: Key('reference-sheet-empty'))
              else
                Paragraphs(paragraphs),
            ],
          ),
        ),
      );
    },
  );
}
