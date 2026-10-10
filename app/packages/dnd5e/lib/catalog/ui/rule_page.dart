import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/ui/markdown_view.dart';
import 'package:opentrpg_core/core/ui/source_chip.dart';

import '../data/catalog_controllers.dart';
import '../data/models.dart' hide Page;
import '../domain/catalog_format.dart';
import 'detail_widgets.dart';

/// A rules document of a content pack: its category, tags and body (light
/// Markdown). It has no mechanical effect.
class RulePage extends ConsumerWidget {
  const RulePage({super.key, required this.index});

  final String index;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rule = ref.watch(ruleDetailProvider(index));
    return Scaffold(
      appBar: AppBar(title: Text(rule.value?.title ?? 'Regla')),
      body: CatalogAsyncBody<Rule>(
        value: rule,
        onRetry: () => ref.invalidate(ruleDetailProvider(index)),
        builder: (r) => DetailList(
          children: [
            Align(alignment: Alignment.centerLeft, child: SourceChip(r.source)),
            if (r.category.isNotEmpty || r.tags.isNotEmpty)
              Text(
                [if (r.category.isNotEmpty) ruleCategoryLabel(r.category), ...r.tags].join(' · '),
                key: const Key('rule-meta'),
                style: Theme.of(context).textTheme.titleSmall,
              ),
            const SizedBox(height: 8),
            MarkdownView(key: const Key('rule-body'), data: r.body.join('\n\n'), selectable: true),
          ],
        ),
      ),
    );
  }
}
