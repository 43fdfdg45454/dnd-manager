import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';
import '../auth/auth_repository.dart';
import '../auth/auth_state.dart';
import '../network/api_client.dart';
import '../network/connectivity.dart';
import '../server/server_config_controller.dart';
import '../server/server_url.dart';
import 'realtime_provider.dart';

/// The four checks of the connection diagnostics, in order.
enum DiagnosticStep {
  /// `GET /health`: the API answers.
  health('API alcanzable'),

  /// `GET /api/v1/auth/me`: the session is valid.
  session('Sesión'),

  /// `POST /hubs/campaign/negotiate`: the hub is reachable through the proxy.
  negotiate('Negociación del hub'),

  /// A real hub connection and the transport it ended up using.
  hub('Conexión en vivo');

  const DiagnosticStep(this.title);

  final String title;
}

enum DiagnosticOutcome { ok, failed, skipped }

/// Result line of one [DiagnosticStep].
class DiagnosticResult {
  const DiagnosticResult({
    required this.step,
    required this.outcome,
    required this.detail,
    this.cause,
  });

  final DiagnosticStep step;
  final DiagnosticOutcome outcome;

  /// What happened: the transports, the HTTP status, the exception.
  final String detail;

  /// Probable cause and what to do (failures and warnings only).
  final String? cause;

  bool get ok => outcome == DiagnosticOutcome.ok;
}

/// A step that got an unexpected HTTP answer ([status]).
class DiagnosticHttpError implements Exception {
  const DiagnosticHttpError(this.status);

  final int status;

  @override
  String toString() => 'HTTP $status';
}

/// A step that ran but whose answer is not what the hub needs.
class DiagnosticFailure implements Exception {
  const DiagnosticFailure(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Where the device is and where the server host points from there: the
/// network in use and the addresses the host resolves to right now (no cache:
/// a fresh lookup). Useful when the same name resolves differently at home
/// (a LAN address) and outside (the public one).
@immutable
class NetworkReport {
  const NetworkReport({required this.network, this.host, this.addresses = const [], this.error});

  /// The interfaces that are up.
  final NetworkInterfaces network;

  /// Host of the saved server (null when there is none).
  final String? host;

  /// What [host] resolves to now.
  final List<String> addresses;

  /// Why the lookup failed.
  final String? error;

  /// "Red: Wi-Fi", "dnd.example.com → 192.0.2.10"...
  String get networkLine => 'Red actual: ${network.label}';

  String? get resolutionLine {
    final host = this.host;
    if (host == null) return null;
    if (error != null) return '$host → no se pudo resolver ($error)';
    return '$host → ${addresses.isEmpty ? 'sin direcciones' : addresses.join(', ')}';
  }

  /// A warning when the answer cannot work from this network: a private
  /// address while on mobile data (the name resolves to the LAN).
  String? get hint {
    if (!network.kinds.contains(NetworkKind.mobile) || network.kinds.contains(NetworkKind.vpn)) {
      return null;
    }
    if (!addresses.any(isPrivateAddress)) return null;
    return 'Con datos móviles el servidor resuelve a una IP privada: fuera de tu red no se '
        'alcanza. Revisa el DNS (debe dar la IP pública fuera de casa).';
  }

  /// Private, loopback or link-local addresses (only reachable inside a LAN).
  static bool isPrivateAddress(String address) {
    final parsed = InternetAddress.tryParse(address);
    if (parsed == null) return false;
    if (parsed.isLoopback || parsed.isLinkLocal) return true;
    final bytes = parsed.rawAddress;
    if (parsed.type == InternetAddressType.IPv4) {
      return bytes[0] == 10 ||
          (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
          (bytes[0] == 192 && bytes[1] == 168) ||
          (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
    }
    // IPv6 unique local addresses (fc00::/7).
    return (bytes[0] & 0xfe) == 0xfc;
  }
}

/// A check that answers with a short description of what it found (the
/// transports, the transport used...) and throws when it fails.
typedef DiagnosticAction = Future<String> Function();

/// Runs the four steps of "Diagnosticar conexión" and explains each failure.
/// The steps are injected so tests can fake them; [diagnosticsProvider] wires
/// the real ones.
class ConnectionDiagnostics {
  const ConnectionDiagnostics({
    required this.health,
    required this.session,
    required this.negotiate,
    required this.hub,
    required this.isSignedIn,
    this.network,
  });

  final DiagnosticAction health;
  final DiagnosticAction session;
  final DiagnosticAction negotiate;

  /// Answers with the transport name the hub connection used.
  final DiagnosticAction hub;

  final bool Function() isSignedIn;

  /// Current network and DNS answer for the server host (shown above the
  /// steps); null hides it.
  final Future<NetworkReport> Function()? network;

  static const signInHint = 'Inicia sesión para probar los pasos 2-4';
  static const proxyHint = 'Tu proxy no reenvía Upgrade/Connection: funciona, pero más lento.';

  /// Runs every step in order; [onProgress] gets the results so far after each
  /// one. Never throws.
  Future<List<DiagnosticResult>> run({void Function(List<DiagnosticResult>)? onProgress}) async {
    final results = <DiagnosticResult>[];
    void add(DiagnosticResult result) {
      results.add(result);
      onProgress?.call(List.unmodifiable(results));
    }

    add(await _run(DiagnosticStep.health, health));

    if (!isSignedIn()) {
      add(
        const DiagnosticResult(
          step: DiagnosticStep.session,
          outcome: DiagnosticOutcome.skipped,
          detail: signInHint,
        ),
      );
      for (final step in [DiagnosticStep.negotiate, DiagnosticStep.hub]) {
        add(DiagnosticResult(step: step, outcome: DiagnosticOutcome.skipped, detail: 'Omitido'));
      }
      return results;
    }

    add(await _run(DiagnosticStep.session, session));
    add(await _run(DiagnosticStep.negotiate, negotiate));
    add(await _run(DiagnosticStep.hub, hub));
    return results;
  }

  Future<DiagnosticResult> _run(DiagnosticStep step, DiagnosticAction action) async {
    try {
      final detail = await action();
      final slow = step == DiagnosticStep.hub && detail != 'WebSockets';
      return DiagnosticResult(
        step: step,
        outcome: DiagnosticOutcome.ok,
        detail: detail,
        cause: slow ? proxyHint : null,
      );
    } catch (error) {
      return DiagnosticResult(
        step: step,
        outcome: DiagnosticOutcome.failed,
        detail: describeFailure(error),
        cause: probableCause(step, error),
      );
    }
  }

  /// HTTP status of [error], if it carries one.
  static int? statusOf(Object error) {
    if (error is DiagnosticHttpError) return error.status;
    if (error is DioException) return error.response?.statusCode;
    return null;
  }

  /// Short text of [error]: the HTTP status or a summary of the exception.
  static String describeFailure(Object error) {
    final status = statusOf(error);
    if (status != null) return 'HTTP $status';
    if (error is DioException) {
      return switch (error.type) {
        DioExceptionType.connectionTimeout ||
        DioExceptionType.sendTimeout ||
        DioExceptionType.receiveTimeout => 'Tiempo de espera agotado',
        DioExceptionType.badCertificate => 'Certificado no válido',
        DioExceptionType.connectionError => 'No se pudo conectar',
        _ => _short('${error.error ?? error.message ?? error.type.name}'),
      };
    }
    return _short('$error');
  }

  static String _short(String text) {
    final line = text.split('\n').first.trim();
    return line.length > 120 ? '${line.substring(0, 117)}...' : line;
  }

  /// Most likely reason a step failed with [error].
  static String probableCause(DiagnosticStep step, Object error) {
    final status = statusOf(error);
    return switch (step) {
      DiagnosticStep.health =>
        status == null
            ? 'El servidor no es alcanzable: revisa la dirección, la red o la VPN.'
            : 'Algo responde, pero no es la API (o está caída): revisa el proxy y que el '
                  'contenedor esté en marcha.',
      DiagnosticStep.session =>
        status == 401
            ? 'La sesión caducó: vuelve a iniciar sesión.'
            : 'La API responde pero no valida la sesión: mira los registros del servidor.',
      DiagnosticStep.negotiate => switch (status) {
        401 || 403 => 'El servidor rechaza el token: vuelve a iniciar sesión.',
        429 => 'Demasiadas peticiones: espera un minuto y vuelve a probar.',
        404 ||
        502 ||
        503 ||
        504 => 'El proxy no reenvía /hubs/: añade esa ruta a la configuración del reverse proxy.',
        _ => 'El hub no responde como se espera: revisa el proxy y los registros del servidor.',
      },
      DiagnosticStep.hub =>
        'El hub negocia pero no se puede abrir la conexión: revisa que el proxy reenvíe '
            'Upgrade/Connection y no corte las conexiones largas.',
    };
  }
}

/// The real checks, against the saved server.
final diagnosticsProvider = Provider<ConnectionDiagnostics>((ref) {
  final dio = ref.read(apiClientProvider).dio;
  final options = Options(
    validateStatus: (_) => true,
    receiveTimeout: const Duration(seconds: 10),
    sendTimeout: const Duration(seconds: 10),
  );

  return ConnectionDiagnostics(
    isSignedIn: () => ref.read(authControllerProvider) is AuthSignedIn,
    health: () async {
      final response = await dio.get<Object?>('/health', options: options);
      final status = response.statusCode ?? 0;
      if (status < 200 || status >= 300) throw DiagnosticHttpError(status);
      return 'HTTP $status';
    },
    session: () async {
      final user = await ref.read(authRepositoryProvider).me();
      return 'Sesión válida (${user.displayName})';
    },
    negotiate: () async {
      await freshAccessToken(ref);
      final response = await dio.post<Object?>(
        '/hubs/campaign/negotiate',
        queryParameters: const {'negotiateVersion': 1},
        options: options.copyWith(responseType: ResponseType.plain),
      );
      final status = response.statusCode ?? 0;
      if (status < 200 || status >= 300) throw DiagnosticHttpError(status);
      final Object? body;
      try {
        body = jsonDecode('${response.data}');
      } catch (_) {
        throw const DiagnosticFailure('Respuesta no válida (no es JSON)');
      }
      final transports = body is Map ? body['availableTransports'] : null;
      if (transports is! List || transports.isEmpty) {
        throw const DiagnosticFailure('Respuesta sin availableTransports');
      }
      final names = [
        for (final t in transports)
          if (t is Map && t['transport'] != null) '${t['transport']}',
      ];
      return 'HTTP $status · transportes: ${names.join(', ')}';
    },
    hub: () => ref.read(realtimeHubProvider).probe(),
    network: () async {
      final network = ref.read(connectivityProvider).network;
      final host = serverHost(ref.read(serverConfigProvider).baseUrl);
      if (host == null) return NetworkReport(network: network);
      try {
        // A fresh lookup every time: nothing in the app caches DNS answers.
        final addresses = await InternetAddress.lookup(host).timeout(const Duration(seconds: 5));
        return NetworkReport(
          network: network,
          host: host,
          addresses: [for (final address in addresses) address.address],
        );
      } catch (error) {
        return NetworkReport(
          network: network,
          host: host,
          error: error is SocketException
              ? error.message
              : ConnectionDiagnostics.describeFailure(error),
        );
      }
    },
  );
});
