import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Makes Dart's `HttpClient` (BoringSSL) trust the CA certificates the user
/// installed on Android (Settings > Security > User credentials), which it
/// ignores by default.
class TrustStore {
  const TrustStore._(this.context, this.certificateCount);

  /// Trusted roots of the system plus the user certificates that could be added.
  final SecurityContext context;

  /// How many user certificates were added to [context].
  final int certificateCount;

  static const MethodChannel channel = MethodChannel('com.opentrpg/trust');

  /// Reads the user CAs from the platform (none off Android or when the call
  /// fails) and builds the context. Never throws.
  ///
  /// [isAndroid] overrides the platform check (tests).
  static Future<TrustStore> load({@visibleForTesting bool? isAndroid}) async {
    var pems = const <String>[];
    if (isAndroid ?? (!kIsWeb && Platform.isAndroid)) {
      try {
        final raw = await channel.invokeListMethod<String>('userCertificates');
        pems = raw ?? const [];
      } catch (error) {
        debugPrint('Could not read user certificates: $error');
      }
    }
    return fromPem(pems);
  }

  /// Builds the context from PEM [certificates]; invalid ones are skipped.
  @visibleForTesting
  static TrustStore fromPem(List<String> certificates) {
    final context = SecurityContext(withTrustedRoots: true);
    var added = 0;
    for (final pem in certificates) {
      try {
        context.setTrustedCertificatesBytes(utf8.encode(pem));
        added++;
      } catch (error) {
        debugPrint('Skipping an invalid user certificate: $error');
      }
    }
    return TrustStore._(context, added);
  }
}

/// Applies [TrustStore.context] to every `HttpClient` created without an
/// explicit context (dio, package:http, Image.network, pdfrx...).
class _TrustHttpOverrides extends HttpOverrides {
  _TrustHttpOverrides(this._context);

  final SecurityContext _context;

  @override
  HttpClient createHttpClient(SecurityContext? context) =>
      super.createHttpClient(context ?? _context);
}

/// Loads the [TrustStore], installs it as [HttpOverrides.global] and returns it.
Future<TrustStore> installGlobalHttpOverrides({@visibleForTesting bool? isAndroid}) async {
  final store = await TrustStore.load(isAndroid: isAndroid);
  HttpOverrides.global = _TrustHttpOverrides(store.context);
  return store;
}

/// Number of user CA certificates the app recognises (overridden in `main`).
final trustedUserCertificateCountProvider = Provider<int>((ref) => 0);
