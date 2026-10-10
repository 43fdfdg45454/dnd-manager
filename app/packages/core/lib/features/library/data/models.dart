import '../../../core/files/stored_file.dart';

/// Category of a library document, in display order.
enum LibraryCategory {
  rules('Rules', 'Reglas'),
  adventure('Adventure', 'Aventuras'),
  supplement('Supplement', 'Suplementos'),
  homebrew('Homebrew', 'Homebrew'),
  other('Other', 'Otros');

  const LibraryCategory(this.apiValue, this.label);

  final String apiValue;
  final String label;

  static LibraryCategory fromApi(Object? value) => LibraryCategory.values.firstWhere(
    (c) => c.apiValue.toLowerCase() == value.toString().toLowerCase(),
    orElse: () => LibraryCategory.other,
  );
}

String? _strOrNull(Object? value) {
  final text = value?.toString() ?? '';
  return text.isEmpty ? null : text;
}

/// A PDF of the instance library.
class LibraryDocument {
  const LibraryDocument({
    required this.id,
    required this.title,
    required this.category,
    required this.fileId,
    required this.url,
    this.description,
    this.fileName = '',
    this.sizeBytes = 0,
    this.pageCount,
    this.isSystem = false,
    this.note,
  });

  factory LibraryDocument.fromJson(Map<String, dynamic> json) {
    final fileId = json['fileId'] as String? ?? '';
    return LibraryDocument(
      id: json['id'] as String,
      title: json['title'] as String? ?? '',
      description: _strOrNull(json['description']),
      category: LibraryCategory.fromApi(json['category']),
      fileId: fileId,
      url: _strOrNull(json['url']) ?? filePath(fileId),
      fileName: json['fileName'] as String? ?? '',
      sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
      pageCount: (json['pageCount'] as num?)?.toInt(),
      isSystem: json['isSystem'] as bool? ?? false,
      note: _strOrNull(json['note']),
    );
  }

  final String id;
  final String title;
  final String? description;
  final LibraryCategory category;
  final String fileId;

  /// Relative download URL of the PDF.
  final String url;
  final String fileName;
  final int sizeBytes;
  final int? pageCount;

  /// Documents provided by the instance (e.g. the SRD) cannot be deleted.
  final bool isSystem;

  /// The DM's note; only in the recommended documents of a campaign.
  final String? note;

  Map<String, dynamic> toJson() => {
    'id': id,
    'title': title,
    'description': description,
    'category': category.apiValue,
    'fileId': fileId,
    'url': url,
    'fileName': fileName,
    'sizeBytes': sizeBytes,
    'pageCount': pageCount,
    'isSystem': isSystem,
    'note': note,
  };
}

/// Documents matching [query] (title or description) and [category] (all when
/// null), keeping the order of [documents].
List<LibraryDocument> filterLibrary(
  List<LibraryDocument> documents, {
  String query = '',
  LibraryCategory? category,
}) {
  final q = query.trim().toLowerCase();
  return [
    for (final d in documents)
      if ((category == null || d.category == category) &&
          (q.isEmpty ||
              d.title.toLowerCase().contains(q) ||
              (d.description ?? '').toLowerCase().contains(q)))
        d,
  ];
}

/// Download state of one document on this device.
enum DownloadStatus { idle, downloading, available }

class DocumentDownload {
  const DocumentDownload(this.status, [this.progress]);

  static const idle = DocumentDownload(DownloadStatus.idle);
  static const available = DocumentDownload(DownloadStatus.available);

  final DownloadStatus status;

  /// Fraction received while [DownloadStatus.downloading]; null if unknown.
  final double? progress;
}

/// Preferences key where the last page read of a document is kept.
String libraryPageKey(String documentId) => 'library.$documentId.page';
