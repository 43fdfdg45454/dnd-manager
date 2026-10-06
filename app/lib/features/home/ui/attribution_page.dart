import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../catalog/data/catalog_controllers.dart';

const _srdFallback =
    'Contenido del System Reference Document 5.1, bajo licencia Creative Commons '
    'Attribution 4.0 International (CC-BY 4.0).';

/// Credits of the third-party material bundled with the app: the SRD 5.1
/// (CC-BY 4.0), the game-icons.net icons (CC BY 3.0) and the OFL fonts.
class AttributionPage extends ConsumerWidget {
  const AttributionPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final srdText = ref.watch(attributionProvider).value?.text;
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Atribuciones')),
      body: ListView(
        key: const Key('attributions-list'),
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          const SectionHeader('Reglas'),
          ParchmentCard(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  srdText == null || srdText.isEmpty ? _srdFallback : srdText,
                  key: const Key('attributions-srd'),
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 8),
                const _LicenseTile(
                  key: Key('attributions-license-cc-by-4'),
                  title: 'Licencia CC-BY 4.0',
                  asset: 'assets/licenses/CC-BY-4.0.txt',
                ),
              ],
            ),
          ),
          const SectionHeader('Iconos'),
          ParchmentCard(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Los iconos proceden de game-icons.net y se usan bajo la licencia '
                  'Creative Commons Attribution 3.0 (CC BY 3.0). Autores: Lorc, Delapouite, '
                  'Skoll y sbed. Se eliminó el fondo negro de cada icono.',
                  key: const Key('attributions-icons'),
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 8),
                const _LicenseTile(
                  key: Key('attributions-license-icons'),
                  title: 'Lista de iconos y autores',
                  asset: 'assets/icons/ATTRIBUTION.md',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-cc-by-3'),
                  title: 'Licencia CC BY 3.0',
                  asset: 'assets/licenses/CC-BY-3.0.txt',
                ),
              ],
            ),
          ),
          const SectionHeader('Tipografías'),
          ParchmentCard(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Cinzel y Alegreya se distribuyen bajo la SIL Open Font License 1.1.',
                  key: const Key('attributions-fonts'),
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 8),
                const _LicenseTile(
                  key: Key('attributions-license-cinzel'),
                  title: 'Licencia de Cinzel',
                  asset: 'assets/licenses/OFL-Cinzel.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-alegreya'),
                  title: 'Licencia de Alegreya',
                  asset: 'assets/licenses/OFL-Alegreya.txt',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Collapsible text of a bundled license / attribution file.
class _LicenseTile extends StatelessWidget {
  const _LicenseTile({super.key, required this.title, required this.asset});

  final String title;
  final String asset;

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      tilePadding: EdgeInsets.zero,
      childrenPadding: const EdgeInsets.only(bottom: 8),
      shape: const Border(),
      collapsedShape: const Border(),
      title: Text(title, style: Theme.of(context).textTheme.titleSmall),
      children: [
        FutureBuilder<String>(
          future: rootBundle.loadString(asset),
          builder: (context, snapshot) {
            final text = snapshot.data ?? (snapshot.hasError ? 'No se pudo cargar el texto.' : '');
            return Align(
              alignment: Alignment.centerLeft,
              child: SelectableText(text, style: Theme.of(context).textTheme.bodySmall),
            );
          },
        ),
      ],
    );
  }
}
