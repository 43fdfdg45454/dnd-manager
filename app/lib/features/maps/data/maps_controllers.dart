import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/content/content_visibility.dart';
import '../../../core/server/app_session_epoch.dart';
import 'maps_repository.dart';
import 'models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// The maps of one campaign. Mutations rethrow errors for the UI.
class MapsController extends AsyncNotifier<List<MapSummary>> {
  MapsController(this.campaignId);

  final String campaignId;

  MapsRepository get _repository => ref.read(mapsRepositoryProvider);

  @override
  Future<List<MapSummary>> build() {
    ref.watch(appSessionEpochProvider);
    return _repository.list(campaignId);
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.list(campaignId));
  }

  /// Creates a map from an uploaded `MapImage` file.
  Future<MapDetail> create({
    required String name,
    required String fileId,
    required ContentVisibility visibility,
  }) async {
    final created = await _repository.create(
      campaignId,
      name: name,
      fileId: fileId,
      visibility: visibility,
    );
    await reload();
    return created;
  }

  Future<void> edit(String id, {String? name, ContentVisibility? visibility}) async {
    await _repository.update(id, name: name, visibility: visibility);
    ref.invalidate(mapDetailControllerProvider(id));
    await reload();
  }

  Future<void> delete(String id) async {
    await _repository.delete(id);
    await reload();
  }
}

final mapsControllerProvider = AsyncNotifierProvider.autoDispose
    .family<MapsController, List<MapSummary>, String>(MapsController.new, retry: _noRetry);

/// One map with its pins. Pin writes update the state from the server's answer;
/// [movePin] is applied at once and undone if the server rejects it.
class MapDetailController extends AsyncNotifier<MapDetail> {
  MapDetailController(this.id);

  final String id;

  MapsRepository get _repository => ref.read(mapsRepositoryProvider);

  @override
  Future<MapDetail> build() => _repository.get(id);

  List<MapPin> get _pins => state.value?.pins ?? const [];

  void _setPins(List<MapPin> pins) {
    final current = state.value;
    if (current != null) state = AsyncData(current.withPins(pins));
  }

  Future<void> reload() async {
    state = AsyncData(await _repository.get(id));
  }

  Future<void> addPin({required double x, required double y, required PinDraft draft}) async {
    final pin = await _repository.createPin(id, x: x, y: y, draft: draft);
    _setPins([..._pins, pin]);
  }

  Future<void> editPin(String pinId, PinDraft draft) async {
    final updated = await _repository.updatePin(id, pinId, draft);
    _setPins([for (final p in _pins) p.id == pinId ? updated : p]);
  }

  Future<void> movePin(String pinId, {required double x, required double y}) async {
    final before = _pins;
    _setPins([for (final p in before) p.id == pinId ? p.copyWith(x: x, y: y) : p]);
    try {
      await _repository.movePin(id, pinId, x: x, y: y);
    } catch (_) {
      _setPins(before);
      rethrow;
    }
  }

  Future<void> deletePin(String pinId) async {
    await _repository.deletePin(id, pinId);
    _setPins(_pins.where((p) => p.id != pinId).toList());
  }
}

final mapDetailControllerProvider = AsyncNotifierProvider.autoDispose
    .family<MapDetailController, MapDetail, String>(MapDetailController.new, retry: _noRetry);
