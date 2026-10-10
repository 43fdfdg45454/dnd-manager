import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/ui/offline_widgets.dart';
import '../data/campaign_content_packs_controller.dart';
import '../domain/campaign_content_pack.dart';
import 'base_pack_chip.dart';

/// "Paquetes de contenido" of a campaign: the packs of its system, the base
/// one always on. With [editable] (Owner/DM, in "Ajustes") each imported pack
/// has a switch and the selection is saved at once; otherwise (the players,
/// in "Contenido") the list is read-only.
class CampaignContentPacksSection extends ConsumerStatefulWidget {
  const CampaignContentPacksSection({super.key, required this.campaignId, required this.editable});

  final String campaignId;
  final bool editable;

  @override
  ConsumerState<CampaignContentPacksSection> createState() => _CampaignContentPacksSectionState();
}

class _CampaignContentPacksSectionState extends ConsumerState<CampaignContentPacksSection> {
  /// The selection being edited; null while it matches the server.
  Set<String>? _draft;
  bool _saving = false;

  static Set<String> _enabledOf(List<CampaignContentPack> packs) => {
    for (final p in packs)
      if (p.enabled && !p.isBase) p.id,
  };

  void _toggle(List<CampaignContentPack> packs, String id, bool on) {
    final next = {...(_draft ?? _enabledOf(packs))};
    on ? next.add(id) : next.remove(id);
    setState(() => _draft = _sameSet(next, _enabledOf(packs)) ? null : next);
  }

  void _enableAlso(List<CampaignContentPack> packs, Iterable<String> ids) {
    final next = {...(_draft ?? _enabledOf(packs)), ...ids};
    setState(() => _draft = _sameSet(next, _enabledOf(packs)) ? null : next);
  }

  static bool _sameSet(Set<String> a, Set<String> b) => a.length == b.length && a.containsAll(b);

  Future<void> _save() async {
    final draft = _draft;
    if (draft == null) return;
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _saving = true);
    try {
      await ref
          .read(campaignContentPacksControllerProvider(widget.campaignId).notifier)
          .save(draft);
      if (!mounted) return;
      setState(() => _draft = null);
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(const SnackBar(content: Text('Paquetes de la campaña guardados.')));
    } catch (error) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(describeContentPacksError(error))));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final value = ref.watch(campaignContentPacksControllerProvider(widget.campaignId));
    return Card(
      key: const Key('campaign-packs'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
              child: Text('Paquetes de contenido', style: theme.textTheme.titleMedium),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                widget.editable
                    ? 'Elige qué paquetes usa la campaña. El asistente, la subida de nivel, '
                          'las tiendas y el compendio de la campaña solo ofrecen su contenido.'
                    : 'Paquetes que usa la campaña. Solo el dueño o un DM pueden cambiarlos.',
                style: theme.textTheme.bodySmall,
              ),
            ),
            value.when(
              skipLoadingOnReload: true,
              loading: () => const Padding(
                padding: EdgeInsets.all(16),
                child: Center(child: CircularProgressIndicator()),
              ),
              error: (error, _) => Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(describeApiError(error), key: const Key('campaign-packs-error')),
                    TextButton.icon(
                      onPressed: () =>
                          ref.invalidate(campaignContentPacksControllerProvider(widget.campaignId)),
                      icon: const Icon(Icons.refresh),
                      label: const Text('Reintentar'),
                    ),
                  ],
                ),
              ),
              data: _list,
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: TextButton.icon(
                key: const Key('campaign-packs-compendium'),
                onPressed: () => context.push(AppRoutes.campaignCompendium(widget.campaignId)),
                icon: const Icon(Icons.menu_book_outlined),
                label: const Text('Ver el compendio de la campaña'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _list(List<CampaignContentPack> packs) {
    final theme = Theme.of(context);
    final names = {for (final p in packs) p.id: p.name};
    final enabled = _draft ?? _enabledOf(packs);
    final missing = missingPackRequirements(packs, enabled);
    final imported = packs.where((p) => !p.isBase).toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final pack in packs)
          _PackTile(
            pack: pack,
            editable: widget.editable && !_saving,
            enabled: pack.isBase || enabled.contains(pack.id),
            requiresText: pack.requires.isEmpty
                ? null
                : 'Requiere: ${pack.requires.map((r) => names[r] ?? r).join(', ')}',
            onChanged: (on) => _toggle(packs, pack.id, on),
          ),
        if (imported.isEmpty)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: Text(
              'No hay paquetes importados en el servidor: solo el contenido base.',
              key: const Key('campaign-packs-empty'),
              style: theme.textTheme.bodySmall,
            ),
          ),
        for (final entry in missing.entries)
          _MissingRequirement(
            key: Key('campaign-pack-missing-${entry.key}'),
            packId: entry.key,
            text:
                '«${names[entry.key] ?? entry.key}» requiere '
                '${entry.value.map((r) => '«${names[r] ?? r}»').join(', ')}: actívalo también.',
            onFix: widget.editable && entry.value.every(names.containsKey)
                ? () => _enableAlso(packs, entry.value)
                : null,
          ),
        if (widget.editable && _draft != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  key: const Key('campaign-packs-discard'),
                  onPressed: _saving ? null : () => setState(() => _draft = null),
                  child: const Text('Descartar'),
                ),
                const SizedBox(width: 8),
                OfflineAware(
                  builder: (context, canWrite) => FilledButton.icon(
                    key: const Key('campaign-packs-save'),
                    onPressed: canWrite && !_saving && missing.isEmpty ? _save : null,
                    icon: _saving
                        ? const SizedBox.square(
                            dimension: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.check),
                    label: const Text('Guardar'),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

class _PackTile extends StatelessWidget {
  const _PackTile({
    required this.pack,
    required this.editable,
    required this.enabled,
    required this.requiresText,
    required this.onChanged,
  });

  final CampaignContentPack pack;
  final bool editable;
  final bool enabled;
  final String? requiresText;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final subtitle = [
      if (pack.version.isNotEmpty) 'Versión ${pack.version}',
      if (pack.isBase) 'Siempre activo' else if (!editable) enabled ? 'Activo' : 'Desactivado',
      ?requiresText,
    ].join(' · ');
    final title = Wrap(
      spacing: 8,
      runSpacing: 2,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [Text(pack.name), if (pack.isBase) const BasePackChip()],
    );
    if (!editable || pack.isBase) {
      return ListTile(
        key: Key('campaign-pack-${pack.id}'),
        leading: Icon(
          enabled ? Icons.check_circle_outline : Icons.remove_circle_outline,
          key: Key('campaign-pack-${pack.id}-${enabled ? 'on' : 'off'}'),
        ),
        title: title,
        subtitle: Text(subtitle),
        enabled: enabled,
      );
    }
    return SwitchListTile(
      key: Key('campaign-pack-${pack.id}'),
      value: enabled,
      onChanged: onChanged,
      title: title,
      subtitle: subtitle.isEmpty ? null : Text(subtitle),
    );
  }
}

class _MissingRequirement extends StatelessWidget {
  const _MissingRequirement({super.key, required this.packId, required this.text, this.onFix});

  final String packId;

  final String text;
  final VoidCallback? onFix;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 4, 16, 4),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: scheme.errorContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: [
          Icon(Icons.warning_amber_rounded, color: scheme.onErrorContainer),
          const SizedBox(width: 8),
          Expanded(
            child: Text(text, style: TextStyle(color: scheme.onErrorContainer)),
          ),
          if (onFix != null)
            TextButton(
              key: Key('campaign-pack-enable-required-$packId'),
              onPressed: onFix,
              child: const Text('Activar'),
            ),
        ],
      ),
    );
  }
}
