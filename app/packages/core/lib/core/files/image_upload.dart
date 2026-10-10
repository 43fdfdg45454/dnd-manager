import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../network/api_error.dart';
import 'files_repository.dart';
import 'stored_file.dart';

/// The gallery picker; a provider so tests can replace it.
final imagePickerProvider = Provider<ImagePicker>((ref) => ImagePicker());

/// Uploads a local file while a modal dialog shows the progress. Returns the
/// stored file, or null (after showing a Spanish error in a SnackBar) when the
/// upload failed.
Future<StoredFile?> uploadWithProgress(
  BuildContext context,
  WidgetRef ref, {
  required String filePath,
  required String fileName,
  required FileKind kind,
  String? campaignId,
  String? characterId,
  String? contentType,
}) async {
  final messenger = ScaffoldMessenger.of(context);
  final navigator = Navigator.of(context, rootNavigator: true);
  final repository = ref.read(filesRepositoryProvider);
  final progress = ValueNotifier<double?>(null);
  var dialogOpen = true;
  showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => PopScope(
      canPop: false,
      child: AlertDialog(
        key: const Key('upload-progress'),
        title: const Text('Subiendo fichero'),
        content: ValueListenableBuilder<double?>(
          valueListenable: progress,
          builder: (_, value, _) => LinearProgressIndicator(value: value),
        ),
      ),
    ),
  ).whenComplete(() => dialogOpen = false);
  try {
    return await repository.upload(
      filePath: filePath,
      fileName: fileName,
      kind: kind,
      campaignId: campaignId,
      characterId: characterId,
      contentType: contentType,
      onProgress: (sent, total) => progress.value = total > 0 ? sent / total : null,
    );
  } catch (error) {
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(describeContentError(error))));
    return null;
  } finally {
    if (dialogOpen) navigator.pop();
    progress.dispose();
  }
}

/// Lets the user pick an image from the gallery and uploads it. Returns null
/// when the user cancels or the upload fails.
Future<StoredFile?> pickAndUploadImage(
  BuildContext context,
  WidgetRef ref, {
  required FileKind kind,
  String? campaignId,
  String? characterId,
  double maxWidth = 2048,
  int quality = 85,
}) async {
  final picked = await ref
      .read(imagePickerProvider)
      .pickImage(source: ImageSource.gallery, maxWidth: maxWidth, imageQuality: quality);
  if (picked == null || !context.mounted) return null;
  return uploadWithProgress(
    context,
    ref,
    filePath: picked.path,
    fileName: picked.name,
    kind: kind,
    campaignId: campaignId,
    characterId: characterId,
    contentType: picked.mimeType,
  );
}
