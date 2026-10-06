import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/catalog/data/catalog_controllers.dart';
import '../../features/catalog/data/models.dart' show CatalogSource;

/// The label shown for the content [source] ("srd", "homebrew" or the id of a
/// content pack), or null when nothing should be shown (the SRD, or an unknown
/// source). [names] maps source ids to their display names.
String? sourceLabel(String? source, Map<String, String> names) {
  if (source == null || source.isEmpty || source == 'srd') return null;
  if (source == 'homebrew') return 'Campaña';
  return names[source] ?? source;
}

/// A small tag with the origin of a piece of content: nothing for the SRD,
/// "Campaña" for homebrew and the pack name for an imported content pack. The
/// name comes from [catalogSourcesProvider]; while it is loading nothing is
/// shown, and when it cannot be loaded the pack id is shown instead.
class SourceChip extends ConsumerWidget {
  const SourceChip(this.source, {super.key});

  final String? source;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final source = this.source;
    if (source == null || source.isEmpty || source == 'srd') return const SizedBox.shrink();
    final String? label;
    if (source == 'homebrew') {
      label = 'Campaña';
    } else {
      final sources = ref.watch(catalogSourcesProvider);
      if (sources.isLoading) return const SizedBox.shrink();
      final names = {for (final s in sources.value ?? const <CatalogSource>[]) s.id: s.name};
      label = sourceLabel(source, names);
    }
    if (label == null) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    return Container(
      key: Key('source-chip-$source'),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: scheme.tertiaryContainer,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(color: scheme.onTertiaryContainer),
      ),
    );
  }
}

/// A [name] followed by the [SourceChip] of [source] (when there is one), for
/// list titles and detail headers.
class NameWithSource extends StatelessWidget {
  const NameWithSource(this.name, this.source, {super.key, this.style});

  final String name;
  final String? source;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 2,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(name, style: style),
        SourceChip(source),
      ],
    );
  }
}
