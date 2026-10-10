/// Thrown by [normalizeServerUrl] with a message ready to show to the user.
class ServerUrlException implements Exception {
  const ServerUrlException(this.message);

  final String message;

  @override
  String toString() => 'ServerUrlException: $message';
}

final _schemePattern = RegExp(r'^[a-zA-Z][a-zA-Z0-9+.\-]*://');
final _hostLabelPattern = RegExp(r'^[a-z0-9_]([a-z0-9_\-]*[a-z0-9_])?$');

/// Normalizes the address typed by the user into a base URL:
///
/// * trims spaces and adds `http://` when there is no scheme;
/// * only `http` and `https` are accepted; scheme and host are lower-cased;
/// * the host must be a name, an IPv4 or an IPv6 address; the port is optional;
/// * trailing slashes, query and fragment are dropped (a path prefix is kept).
///
/// Throws a [ServerUrlException] (message in Spanish) when it is not usable.
String normalizeServerUrl(String input) {
  var text = input.trim();
  if (text.isEmpty) {
    throw const ServerUrlException('Introduce la dirección del servidor.');
  }
  if (text.contains(RegExp(r'\s'))) {
    throw const ServerUrlException('La dirección no puede contener espacios.');
  }

  final hasScheme = _schemePattern.hasMatch(text);
  if (!hasScheme) text = 'http://$text';

  final Uri uri;
  try {
    uri = Uri.parse(text);
  } on FormatException {
    throw const ServerUrlException('La dirección no es válida.');
  }

  if (uri.scheme != 'http' && uri.scheme != 'https') {
    throw const ServerUrlException('Solo se admiten direcciones http:// o https://.');
  }
  if (uri.userInfo.isNotEmpty) {
    throw const ServerUrlException('No incluyas usuario ni contraseña en la dirección.');
  }
  final host = uri.host;
  if (host.isEmpty || !_isValidHost(host)) {
    throw const ServerUrlException('El nombre o la IP del servidor no es válido.');
  }
  if (uri.hasPort && (uri.port < 1 || uri.port > 65535)) {
    throw const ServerUrlException('El puerto debe estar entre 1 y 65535.');
  }

  final path = uri.path.replaceAll(RegExp(r'/+$'), '');
  final shownHost = host.contains(':') ? '[$host]' : host;
  final port = uri.hasPort ? ':${uri.port}' : '';
  return '${uri.scheme}://$shownHost$port$path';
}

/// Returns the validation message for [input], or null when it is valid.
String? validateServerUrl(String? input) {
  try {
    normalizeServerUrl(input ?? '');
    return null;
  } on ServerUrlException catch (e) {
    return e.message;
  }
}

/// Lower-cased host of [baseUrl], or null when it cannot be parsed.
String? serverHost(String baseUrl) {
  final host = Uri.tryParse(baseUrl)?.host;
  return (host == null || host.isEmpty) ? null : host.toLowerCase();
}

bool _isValidHost(String host) {
  if (host.contains(':')) return true; // IPv6 literal, already parsed by Uri.
  final labels = host.split('.');
  if (labels.any((l) => l.isEmpty)) return false;
  return labels.every((l) => _hostLabelPattern.hasMatch(l.toLowerCase()));
}
