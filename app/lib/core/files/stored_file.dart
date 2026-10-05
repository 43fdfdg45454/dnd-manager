/// What an uploaded file is for, as the server expects it in the `kind` field
/// of `POST /api/v1/files`.
enum FileKind {
  mapImage('MapImage'),
  portrait('Portrait'),
  loreAttachment('LoreAttachment'),
  libraryDocument('LibraryDocument');

  const FileKind(this.apiValue);

  final String apiValue;
}

/// Result of `POST /api/v1/files`.
class StoredFile {
  const StoredFile({
    required this.id,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
    required this.url,
  });

  factory StoredFile.fromJson(Map<String, dynamic> json) => StoredFile(
    id: json['id'] as String,
    fileName: json['fileName'] as String? ?? '',
    contentType: json['contentType'] as String? ?? '',
    sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
    url: json['url'] as String? ?? filePath(json['id'] as String),
  );

  final String id;
  final String fileName;
  final String contentType;
  final int sizeBytes;

  /// Relative download URL (`/api/v1/files/{id}`); needs the bearer token.
  final String url;
}

/// Relative download URL of the stored file [id].
String filePath(String id) => '/api/v1/files/$id';

/// Content type for a file name, from its extension, for the types the server
/// accepts. Falls back to `application/octet-stream`.
String contentTypeForFileName(String fileName) {
  final dot = fileName.lastIndexOf('.');
  final extension = dot < 0 ? '' : fileName.substring(dot + 1).toLowerCase();
  return switch (extension) {
    'png' => 'image/png',
    'jpg' || 'jpeg' => 'image/jpeg',
    'webp' => 'image/webp',
    'pdf' => 'application/pdf',
    _ => 'application/octet-stream',
  };
}

/// Absolute URL for [url], which the API sends relative to the server
/// ([baseUrl]); already absolute URLs are returned untouched.
String resolveFileUrl(String baseUrl, String url) {
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  final base = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
  return url.startsWith('/') ? '$base$url' : '$base/$url';
}

/// Human-readable size, e.g. `2,4 MB`.
String formatFileSize(int bytes) {
  String one(double value) => value.toStringAsFixed(1).replaceAll('.', ',');
  if (bytes < 1024) return '$bytes B';
  if (bytes < 1024 * 1024) return '${one(bytes / 1024)} KB';
  if (bytes < 1024 * 1024 * 1024) return '${one(bytes / (1024 * 1024))} MB';
  return '${one(bytes / (1024 * 1024 * 1024))} GB';
}
