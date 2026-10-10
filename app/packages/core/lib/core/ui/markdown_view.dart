import 'package:flutter/material.dart';
import 'package:flutter_markdown_plus/flutter_markdown_plus.dart';

import '../files/authenticated_image.dart';

/// Scheme of the links built from `[[slug]]` references.
const loreLinkScheme = 'lore';

final _wikiLink = RegExp(r'\[\[([^\[\]|\n]+?)(?:\|([^\[\]\n]+?))?\]\]');

/// Rewrites `[[slug]]` and `[[slug|text]]` as markdown links with the
/// `lore:<slug>` scheme. The text is [titles] (title by slug) when the slug is
/// known, otherwise the slug itself.
String expandWikiLinks(String markdown, {Map<String, String> titles = const {}}) {
  return markdown.replaceAllMapped(_wikiLink, (match) {
    final slug = match.group(1)!.trim();
    if (slug.isEmpty) return match.group(0)!;
    final label = (match.group(2) ?? titles[slug] ?? slug).trim().replaceAll(']', r'\]');
    return '[$label]($loreLinkScheme:${Uri.encodeComponent(slug)})';
  });
}

/// The slug of a `lore:<slug>` link target, or null for any other link.
String? loreSlugOfHref(String? href) {
  if (href == null || !href.startsWith('$loreLinkScheme:')) return null;
  return Uri.decodeComponent(href.substring(loreLinkScheme.length + 1));
}

/// Renders markdown. `[[slug]]` references become links and [onLoreLink] is
/// called with the slug when one is tapped; images that point to the server
/// (`/api/v1/files/{id}`) are loaded with the session token.
class MarkdownView extends StatelessWidget {
  const MarkdownView({
    super.key,
    required this.data,
    this.titles = const {},
    this.onLoreLink,
    this.selectable = false,
  });

  final String data;

  /// Entry titles by slug, used as the text of `[[slug]]` links.
  final Map<String, String> titles;
  final ValueChanged<String>? onLoreLink;
  final bool selectable;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return MarkdownBody(
      data: expandWikiLinks(data, titles: titles),
      selectable: selectable,
      styleSheet: MarkdownStyleSheet.fromTheme(theme).copyWith(
        a: TextStyle(color: theme.colorScheme.primary, decoration: TextDecoration.underline),
      ),
      onTapLink: (text, href, title) {
        final slug = loreSlugOfHref(href);
        if (slug != null) onLoreLink?.call(slug);
      },
      imageBuilder: (uri, title, alt) {
        final isServerFile = !uri.hasScheme && uri.path.startsWith('/api/v1/files/');
        if (!isServerFile) return Text(alt ?? '', style: theme.textTheme.bodySmall);
        return Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: AuthenticatedImage(url: uri.path, fit: BoxFit.contain, height: 220),
        );
      },
    );
  }
}
