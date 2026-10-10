import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/catalog/catalog_sources.dart';
import '../../../core/network/api_error.dart';
import '../domain/campaign_content_pack.dart';
import 'campaign_content_packs_repository.dart';

/// The content packs of a campaign and which ones it enables.
class CampaignContentPacksController extends AsyncNotifier<List<CampaignContentPack>> {
  CampaignContentPacksController(this.campaignId);

  final String campaignId;

  CampaignContentPacksRepository get _repository =>
      ref.read(campaignContentPacksRepositoryProvider);

  @override
  Future<List<CampaignContentPack>> build() {
    ref.watch(catalogRevisionProvider);
    return ref.watch(campaignContentPacksRepositoryProvider).list(campaignId);
  }

  /// Enables exactly [packIds] (the base pack is always on) and reloads the
  /// catalog of the campaign. Errors are rethrown.
  Future<void> save(Set<String> packIds) async {
    final packs = await _repository.setEnabled(campaignId, packIds);
    state = AsyncData(packs);
    ref.read(campaignCatalogRevisionProvider(campaignId).notifier).bump();
  }
}

final campaignContentPacksControllerProvider = AsyncNotifierProvider.autoDispose
    .family<CampaignContentPacksController, List<CampaignContentPack>, String>(
      CampaignContentPacksController.new,
      retry: (retryCount, error) => null,
    );

/// The Spanish message of a failed save of the packs of a campaign.
String describeContentPacksError(Object error) {
  if (error is DioException && error.response?.statusCode == 400) {
    final detail = problemDetail(error);
    return switch (problemCode(error)) {
      'missing-requirement' => detail ?? 'Un paquete necesita otro que no está activo.',
      'unknown-pack' => detail ?? 'Uno de los paquetes ya no existe.',
      _ => detail ?? 'La selección de paquetes no es válida.',
    };
  }
  return describeApiError(
    error,
    byStatus: const {
      403: 'Solo el dueño o un DM pueden cambiar los paquetes de la campaña.',
      404: 'La campaña no existe o no tienes acceso.',
    },
  );
}
