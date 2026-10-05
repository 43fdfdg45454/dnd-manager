import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../domain/catalog_format.dart';

const _notFoundMessage = 'No se encontró este elemento del compendio.';

/// Shows [value] with the standard loading and error (retry) states.
class CatalogAsyncBody<T> extends StatelessWidget {
  const CatalogAsyncBody({
    super.key,
    required this.value,
    required this.onRetry,
    required this.builder,
  });

  final AsyncValue<T> value;
  final VoidCallback onRetry;
  final Widget Function(T data) builder;

  @override
  Widget build(BuildContext context) {
    return value.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => CatalogErrorView(error: error, onRetry: onRetry),
      data: builder,
    );
  }
}

class CatalogErrorView extends StatelessWidget {
  const CatalogErrorView({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              describeApiError(error, byStatus: const {404: _notFoundMessage}),
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

/// Scrollable detail layout with a standard padding.
class DetailList extends StatelessWidget {
  const DetailList({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: EdgeInsets.fromLTRB(16, 8, 16, 24 + MediaQuery.paddingOf(context).bottom),
      children: children,
    );
  }
}

class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 20, bottom: 8),
      child: Text(text, style: Theme.of(context).textTheme.titleMedium),
    );
  }
}

/// "Label: value" row. Renders nothing when [value] is null or empty.
class FactRow extends StatelessWidget {
  const FactRow(this.label, this.value, {super.key});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    final text = value;
    if (text == null || text.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Text.rich(
        TextSpan(
          children: [
            TextSpan(
              text: '$label: ',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            TextSpan(text: text),
          ],
        ),
        style: theme.textTheme.bodyMedium,
      ),
    );
  }
}

/// One selectable paragraph per entry.
class Paragraphs extends StatelessWidget {
  const Paragraphs(this.texts, {super.key});

  final List<String> texts;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.bodyMedium;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final text in texts)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: SelectableText(cleanText(text), style: style),
          ),
      ],
    );
  }
}

/// Collapsible entry (name, optional subtitle) with description paragraphs.
class ExpandableEntry extends StatelessWidget {
  const ExpandableEntry({
    super.key,
    required this.title,
    this.subtitle,
    required this.description,
    this.emptyText = 'Sin descripción.',
  });

  final String title;
  final String? subtitle;
  final List<String> description;
  final String emptyText;

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      tilePadding: EdgeInsets.zero,
      childrenPadding: const EdgeInsets.only(bottom: 8),
      expandedCrossAxisAlignment: CrossAxisAlignment.start,
      title: Text(title),
      subtitle: subtitle == null ? null : Text(subtitle!),
      children: [description.isEmpty ? Text(emptyText) : Paragraphs(description)],
    );
  }
}
