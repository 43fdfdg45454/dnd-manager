import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/cache/stale_data.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/icons.dart';
import '../../../../core/ui/offline_widgets.dart';
import '../../../characters/ui/characters_tab.dart';
import '../../../items/ui/homebrew_tab.dart';
import '../../../items/ui/shops_tab.dart';
import '../../../library/ui/library_page.dart';
import '../../../lore/ui/lore_tab.dart';
import '../../../maps/ui/maps_tab.dart';
import '../../../sessions/ui/journal_tab.dart';
import '../../../sessions/ui/sessions_tab.dart';
import '../../data/campaigns_controller.dart';
import '../../data/campaigns_repository.dart';
import '../../domain/campaign_models.dart';
import '../members_section.dart';
import 'campaign_settings_section.dart';

/// Sections of the "Campaña" view of a campaign, in display order. Each one is
/// a full page at `/campaigns/:id/general/<path>` with the key `general-<path>`
/// on its card, except the characters, which are a branch of the campaign bar.
enum CampaignSection {
  characters('characters', 'Personajes', AppIcons.hood),
  lore('lore', 'Lore', AppIcons.book),
  maps('maps', 'Mapas', AppIcons.map),
  sessions('sessions', 'Sesiones', AppIcons.calendar),
  journal('journal', 'Diario', AppIcons.quill),
  members('members', 'Miembros', AppIcons.users),
  shops('shops', 'Tiendas', AppIcons.coins),
  library('library', 'Biblioteca', AppIcons.scroll),
  content('content', 'Contenido', AppIcons.anvil),
  settings('settings', 'Ajustes', AppIcons.castle);

  const CampaignSection(this.path, this.label, this.icon);

  /// Last segment of the location.
  final String path;

  /// Spanish title of the card and the page.
  final String label;
  final AppIcons icon;
}

/// A section of the "General" view as a full page: an app bar with the section
/// name over the existing widget of the section.
class CampaignSectionPage extends ConsumerWidget {
  const CampaignSectionPage({super.key, required this.campaignId, required this.section});

  final String campaignId;
  final CampaignSection section;

  static Widget _content(CampaignSection section, CampaignDetail campaign) => switch (section) {
    CampaignSection.characters => CharactersTab(campaign: campaign),
    CampaignSection.lore => LoreTab(campaign: campaign),
    CampaignSection.maps => MapsTab(campaign: campaign),
    CampaignSection.sessions => SessionsTab(campaign: campaign),
    CampaignSection.journal => JournalTab(campaign: campaign),
    CampaignSection.members => MembersSection(campaign: campaign),
    CampaignSection.shops => ShopsTab(campaign: campaign),
    // The library has its own page (app bar and filters).
    CampaignSection.library => const SizedBox.shrink(),
    CampaignSection.content => HomebrewTab(campaign: campaign),
    CampaignSection.settings => CampaignSettingsSection(campaign: campaign),
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (section == CampaignSection.library) return LibraryPage(campaignId: campaignId);
    final detail = ref.watch(campaignDetailControllerProvider(campaignId));
    return Scaffold(
      key: Key('section-${section.path}'),
      appBar: AppBar(title: Text(section.label)),
      body: OfflineBannerLayout(
        scopes: [staleTree(CampaignsRepository.campaignPath(campaignId))],
        child: detail.when(
          skipLoadingOnReload: true,
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(describeCampaignError(error), textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: () => ref.invalidate(campaignDetailControllerProvider(campaignId)),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Reintentar'),
                  ),
                ],
              ),
            ),
          ),
          data: (campaign) => _content(section, campaign),
        ),
      ),
    );
  }
}
