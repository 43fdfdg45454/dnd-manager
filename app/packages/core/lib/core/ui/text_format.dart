/// Drops the Markdown emphasis and heading marks that rules datasets (the SRD,
/// content packs) keep in descriptions.
String cleanText(String text) => text
    .replaceAll('**', '')
    .replaceAll(RegExp(r'^#+\s*', multiLine: true), '')
    .replaceAll(RegExp(r'(?<![A-Za-z0-9])_(.+?)_(?![A-Za-z0-9])'), r'$1')
    .trim();
