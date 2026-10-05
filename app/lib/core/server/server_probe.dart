import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'certificate_pinning.dart';

/// Name and version reported by a reachable D&D Companion server.
class ServerProbeResult {
  const ServerProbeResult({required this.name, required this.version});

  final String name;
  final String version;
}

enum ServerProbeFailure {
  /// No answer in time.
  timeout,

  /// The host name could not be resolved.
  dns,

  /// The HTTPS certificate is not trusted (or differs from the pinned one).
  certificate,

  /// The host answered but it is not a D&D Companion API.
  notAServer,

  /// Connection refused, network down, or a server-side error.
  unreachable,
}

/// A failed probe, with a Spanish [message] for the UI.
class ServerProbeException implements Exception {
  const ServerProbeException(this.failure, {this.fingerprint, this.hadPinnedCertificate = false});

  final ServerProbeFailure failure;

  /// SHA-256 fingerprint of the certificate the server presented, for
  /// [ServerProbeFailure.certificate] (null when it could not be read).
  final String? fingerprint;

  /// The failing host already had a pinned certificate, so the presented one
  /// has changed.
  final bool hadPinnedCertificate;

  String get message => switch (failure) {
    ServerProbeFailure.timeout => 'El servidor no respondió a tiempo. Comprueba la dirección y que estés en la red o la VPN correcta.',
    ServerProbeFailure.dns => 'No se encontró el servidor. Revisa el nombre o la dirección IP.',
    ServerProbeFailure.certificate =>
      hadPinnedCertificate
          ? 'El certificado del servidor ha cambiado y no coincide con el que habías aceptado.'
          : 'El certificado del servidor no es de confianza.',
    ServerProbeFailure.notAServer => 'No parece un servidor de D&D Companion.',
    ServerProbeFailure.unreachable => 'No se pudo conectar con el servidor. Comprueba que esté encendido y que estés en la misma red o VPN.',
  };

  @override
  String toString() => 'ServerProbeException($failure)';
}

/// Checks `GET /api/v1/app/info` on an arbitrary URL with a throwaway client
/// (the app client keeps pointing at the saved server).
class ServerProbe {
  const ServerProbe({this.adapterFactory});

  /// Replaces the HTTP adapter (tests).
  final HttpClientAdapter Function()? adapterFactory;

  /// Throws [ServerProbeException] when the server cannot be verified.
  /// [pinnedFingerprint] is the certificate the user already trusted for this
  /// URL's host, if any.
  Future<ServerProbeResult> check(String baseUrl, {String? pinnedFingerprint}) async {
    final uri = Uri.tryParse(baseUrl);
    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 8),
        receiveTimeout: const Duration(seconds: 8),
        headers: const {'Accept': 'application/json'},
      ),
    );

    String? presentedFingerprint;
    if (adapterFactory != null) {
      dio.httpClientAdapter = adapterFactory!();
    } else if (uri != null && uri.scheme == 'https') {
      dio.httpClientAdapter = IOHttpClientAdapter(
        createHttpClient: () {
          final client = HttpClient();
          client.badCertificateCallback = (cert, host, port) {
            final fingerprint = certificateFingerprint(cert.der);
            presentedFingerprint = fingerprint;
            return pinnedFingerprint != null &&
                host.toLowerCase() == uri.host.toLowerCase() &&
                fingerprintsMatch(fingerprint, pinnedFingerprint);
          };
          return client;
        },
      );
    }

    try {
      final response = await dio.get<Object?>('/api/v1/app/info');
      final data = response.data;
      if (data is Map && data['name'] is String && data['version'] is String) {
        return ServerProbeResult(name: data['name'] as String, version: data['version'] as String);
      }
      throw const ServerProbeException(ServerProbeFailure.notAServer);
    } on DioException catch (e) {
      throw _classify(e, fingerprint: presentedFingerprint, hadPinned: pinnedFingerprint != null);
    } finally {
      dio.close(force: true);
    }
  }

  ServerProbeException _classify(
    DioException e, {
    required String? fingerprint,
    required bool hadPinned,
  }) {
    final error = e.error;
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return const ServerProbeException(ServerProbeFailure.timeout);
      case DioExceptionType.badCertificate:
        return ServerProbeException(
          ServerProbeFailure.certificate,
          fingerprint: fingerprint,
          hadPinnedCertificate: hadPinned,
        );
      case DioExceptionType.badResponse:
        final status = e.response?.statusCode ?? 0;
        return status >= 500
            ? const ServerProbeException(ServerProbeFailure.unreachable)
            : const ServerProbeException(ServerProbeFailure.notAServer);
      case DioExceptionType.connectionError:
      case DioExceptionType.unknown:
      case DioExceptionType.cancel:
        if (error is HandshakeException || error is TlsException || error is CertificateException) {
          return ServerProbeException(
            ServerProbeFailure.certificate,
            fingerprint: fingerprint,
            hadPinnedCertificate: hadPinned,
          );
        }
        if (error is SocketException &&
            error.message.toLowerCase().contains('failed host lookup')) {
          return const ServerProbeException(ServerProbeFailure.dns);
        }
        return const ServerProbeException(ServerProbeFailure.unreachable);
    }
  }
}

final serverProbeProvider = Provider<ServerProbe>((ref) => const ServerProbe());
