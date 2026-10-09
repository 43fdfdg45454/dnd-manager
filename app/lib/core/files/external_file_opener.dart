import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:open_filex/open_filex.dart';

/// What happened when a local file was handed to the system.
enum ExternalOpenResult {
  /// An app of the system took the file.
  opened,

  /// No installed app can open that type of file.
  noApp,

  /// The file is missing or the system refused it.
  failed,
}

/// Opens local files with the app the user picks in the system (Android
/// `ACTION_VIEW` through `open_filex`, which shares the file with its own
/// FileProvider; files inside the app's private storage need no permission).
class ExternalFileOpener {
  const ExternalFileOpener();

  Future<ExternalOpenResult> open(String path, {String? mimeType}) async {
    try {
      final result = await OpenFilex.open(path, type: mimeType);
      return switch (result.type) {
        ResultType.done => ExternalOpenResult.opened,
        ResultType.noAppToOpen => ExternalOpenResult.noApp,
        _ => ExternalOpenResult.failed,
      };
    } catch (_) {
      // No platform implementation (tests, unsupported desktop setups).
      return ExternalOpenResult.failed;
    }
  }
}

final externalFileOpenerProvider = Provider<ExternalFileOpener>(
  (ref) => const ExternalFileOpener(),
);
