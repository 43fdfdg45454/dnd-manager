/// The latest APK published on the server (`GET /api/v1/app/latest`).
class AppRelease {
  const AppRelease({
    required this.version,
    required this.buildNumber,
    required this.notes,
    required this.isMandatory,
    required this.downloadUrl,
    required this.sizeBytes,
    this.publishedAt,
  });

  factory AppRelease.fromJson(Map<String, dynamic> json) => AppRelease(
    version: json['version'] as String,
    buildNumber: (json['buildNumber'] as num).toInt(),
    notes: json['notes'] as String? ?? '',
    isMandatory: json['isMandatory'] as bool? ?? false,
    downloadUrl: json['downloadUrl'] as String,
    sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
    publishedAt: json['publishedAt'] == null
        ? null
        : DateTime.tryParse(json['publishedAt'] as String),
  );

  /// Semantic version shown to the user ("1.2.0").
  final String version;

  /// Compared with the build number of the installed app.
  final int buildNumber;
  final String notes;

  /// The app cannot be used until this version is installed.
  final bool isMandatory;

  /// Absolute URL of the anonymous APK download.
  final String downloadUrl;
  final int sizeBytes;
  final DateTime? publishedAt;

  AppRelease withDownloadUrl(String url) => AppRelease(
    version: version,
    buildNumber: buildNumber,
    notes: notes,
    isMandatory: isMandatory,
    downloadUrl: url,
    sizeBytes: sizeBytes,
    publishedAt: publishedAt,
  );

  Map<String, dynamic> toJson() => {
    'version': version,
    'buildNumber': buildNumber,
    'notes': notes,
    'isMandatory': isMandatory,
    'downloadUrl': downloadUrl,
    'sizeBytes': sizeBytes,
    'publishedAt': publishedAt?.toIso8601String(),
  };
}

/// "12,3 MB" (or KB for small files).
String formatReleaseSize(int bytes) {
  if (bytes <= 0) return '';
  const mb = 1024 * 1024;
  if (bytes < mb) return '${(bytes / 1024).ceil()} KB';
  return '${(bytes / mb).toStringAsFixed(1).replaceAll('.', ',')} MB';
}
