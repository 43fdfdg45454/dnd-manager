import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/theme/app_theme.dart';

/// Credits of the third-party material bundled with the app: the rules
/// content of each game system (D&D 5e: the SRD 5.1 under CC-BY 4.0) and the
/// OFL fonts. The icons are original work of the project.
class AttributionPage extends ConsumerWidget {
  const AttributionPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final credits = [
      for (final system in ref.watch(gameSystemsProvider)) ...system.attributions,
    ];
    return Scaffold(
      appBar: AppBar(title: const Text('Atribuciones')),
      body: ListView(
        key: const Key('attributions-list'),
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          if (credits.isNotEmpty) ...[
            const SectionHeader('Reglas'),
            for (final credit in credits) _RulesCredit(credit: credit),
          ],
          const SectionHeader('Tipografías'),
          ParchmentCard(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Almendra, Cinzel, IM Fell English, Source Sans 3, Atkinson Hyperlegible Next '
                  'y Lora se distribuyen bajo la SIL Open Font License 1.1.',
                  key: const Key('attributions-fonts'),
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 8),
                const _LicenseTile(
                  key: Key('attributions-license-almendra'),
                  title: 'Licencia de Almendra',
                  asset: 'assets/licenses/OFL-Almendra.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-source-sans'),
                  title: 'Licencia de Source Sans 3',
                  asset: 'assets/licenses/OFL-SourceSans3.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-cinzel'),
                  title: 'Licencia de Cinzel',
                  asset: 'assets/licenses/OFL-Cinzel.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-im-fell-english'),
                  title: 'Licencia de IM Fell English',
                  asset: 'assets/licenses/OFL-IMFellEnglish.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-atkinson'),
                  title: 'Licencia de Atkinson Hyperlegible Next',
                  asset: 'assets/licenses/OFL-AtkinsonHyperlegibleNext.txt',
                ),
                const _LicenseTile(
                  key: Key('attributions-license-lora'),
                  title: 'Licencia de Lora',
                  asset: 'assets/licenses/OFL-Lora.txt',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The credit of the rules content of a game system and its license.
class _RulesCredit extends ConsumerWidget {
  const _RulesCredit({required this.credit});

  final SystemAttribution credit;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverText = credit.serverText;
    final text = serverText == null ? null : ref.watch(serverText).value;
    return ParchmentCard(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            text == null || text.isEmpty ? credit.text : text,
            key: Key('attributions-${credit.id}'),
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 8),
          _LicenseTile(
            key: Key('attributions-license-${credit.licenseId}'),
            title: credit.licenseTitle,
            asset: credit.licenseAsset,
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
