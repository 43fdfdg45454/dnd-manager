import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/server/app_session_epoch.dart';
import 'lore_repository.dart';
import 'models.dart';

/// Errors are shown with a retry button instead of being retried silently.
Duration? _noRetry(int retryCount, Object error) => null;

/// The lore tree of one campaign. Mutations rethrow errors for the UI.
class LoreController extends AsyncNotifier<List<LoreSummary>> {
  LoreController(this.campaignId);

  final String campaignId;

  LoreRepository get _repository => ref.read(loreRepositoryProvider);

  @override
  Future<List<LoreSummary>> build() {
    ref.watch(appSessionEpochProvider);
    return _repository.list(campaignId);
  }

  Future<void> reload() async {
    state = await AsyncValue.guard(() => _repository.list(campaignId));
  }

  /// Creates an entry and, if given, attaches the uploaded files [attachFileIds].
  Future<LoreEntry> create(LoreDraft draft, {List<String> attachFileIds = const []}) async {
    final created = await _repository.create(campaignId, draft);
    for (final fileId in attachFileIds) {
      await _repository.addAttachment(created.id, fileId: fileId);
    }
    await reload();
    return created;
  }

  /// Saves an edit, then attaches [addFileIds] and removes [removeAttachmentIds].
  Future<void> save(
    String id,
    LoreDraft draft, {
    List<String> addFileIds = const [],
    List<String> removeAttachmentIds = const [],
  }) async {
    await _repository.update(id, draft);
    for (final fileId in addFileIds) {
      await _repository.addAttachment(id, fileId: fileId);
    }
    for (final attachmentId in removeAttachmentIds) {
      await _repository.removeAttachment(id, attachmentId);
    }
    ref.invalidate(loreEntryControllerProvider(id));
    await reload();
  }

  Future<void> delete(String id) async {
    await _repository.delete(id);
    await reload();
  }
}

final loreControllerProvider = AsyncNotifierProvider.autoDispose
    .family<LoreController, List<LoreSummary>, String>(LoreController.new, retry: _noRetry);

/// One entry with its content and attachments.
class LoreEntryController extends AsyncNotifier<LoreEntry> {
  LoreEntryController(this.id);

  final String id;

  @override
  Future<LoreEntry> build() => ref.read(loreRepositoryProvider).get(id);
}

final loreEntryControllerProvider = AsyncNotifierProvider.autoDispose
    .family<LoreEntryController, LoreEntry, String>(LoreEntryController.new, retry: _noRetry);
