import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:dio/io.dart';

/// SHA-256 fingerprint of a DER certificate as uppercase hex pairs separated by
/// colons (the format shown to the user).
String certificateFingerprint(List<int> der) => sha256
    .convert(der)
    .bytes
    .map((b) => b.toRadixString(16).padLeft(2, '0').toUpperCase())
    .join(':');

/// Whether two fingerprints are equal ignoring case and separators.
bool fingerprintsMatch(String a, String b) => _canonical(a) == _canonical(b);

String _canonical(String value) => value.replaceAll(RegExp(r'[^0-9a-fA-F]'), '').toLowerCase();

/// Adapter that accepts an otherwise untrusted certificate only when it is
/// presented by [host] and its SHA-256 fingerprint equals [fingerprint].
/// Verification stays on for every other host.
HttpClientAdapter pinnedCertificateAdapter({required String host, required String fingerprint}) {
  final pinnedHost = host.toLowerCase();
  return IOHttpClientAdapter(
    createHttpClient: () {
      final client = HttpClient();
      client.badCertificateCallback = (X509Certificate cert, String requestHost, int port) =>
          requestHost.toLowerCase() == pinnedHost &&
          fingerprintsMatch(certificateFingerprint(cert.der), fingerprint);
      return client;
    },
  );
}
