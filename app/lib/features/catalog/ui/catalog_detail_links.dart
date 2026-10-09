import 'package:flutter/material.dart';

import 'beast_page.dart';
import 'class_detail_page.dart';
import 'item_detail_page.dart';
import 'race_detail_page.dart';
import 'spell_detail_page.dart';

// Shortcuts to the compendium detail pages from any choice screen. They use
// Navigator (not the router) so the detail opens on top of pickers that were
// themselves pushed with Navigator, and the back arrow returns to them.

Future<void> openSpellDetail(BuildContext context, String index) =>
    _open(context, SpellDetailPage(index: index));

Future<void> openItemDetail(BuildContext context, String templateId) =>
    _open(context, ItemDetailPage(id: templateId));

Future<void> openRaceDetail(BuildContext context, String index) =>
    _open(context, RaceDetailPage(index: index));

Future<void> openClassDetail(BuildContext context, String index) =>
    _open(context, ClassDetailPage(index: index));

Future<void> openBeastDetail(BuildContext context, String index) =>
    _open(context, BeastPage(index: index));

Future<void> _open(BuildContext context, Widget page) =>
    Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => page));

/// Compact info button that opens the detail of a catalog element.
class DetailInfoButton extends StatelessWidget {
  const DetailInfoButton({super.key, required this.onPressed, this.tooltip = 'Ver detalle'});

  final VoidCallback onPressed;
  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: const Icon(Icons.info_outline),
      tooltip: tooltip,
      onPressed: onPressed,
      visualDensity: VisualDensity.compact,
      constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
      padding: EdgeInsets.zero,
      iconSize: 20,
    );
  }
}
