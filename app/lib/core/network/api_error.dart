import 'dart:io';

import 'package:dio/dio.dart';

const networkErrorMessage = 'No se pudo conectar con el servidor. Revisa tu conexión.';

/// Maps an error to a Spanish message for the UI. [byStatus] overrides the
/// default text for specific HTTP status codes.
String describeApiError(Object error, {Map<int, String> byStatus = const {}}) {
  if (error is! DioException) {
    return 'Ocurrió un error inesperado. Inténtalo de nuevo.';
  }

  final status = error.response?.statusCode;
  if (status != null && byStatus.containsKey(status)) {
    return byStatus[status]!;
  }

  const networkTypes = {
    DioExceptionType.connectionError,
    DioExceptionType.connectionTimeout,
    DioExceptionType.sendTimeout,
    DioExceptionType.receiveTimeout,
    DioExceptionType.badCertificate,
  };
  if (networkTypes.contains(error.type) || error.error is SocketException) {
    return networkErrorMessage;
  }

  return switch (status) {
    400 => 'Los datos enviados no son válidos.',
    401 => 'Tu sesión ha caducado. Inicia sesión de nuevo.',
    403 => 'No tienes permisos para realizar esta acción.',
    404 => 'No se encontró el recurso solicitado.',
    409 => 'La operación entra en conflicto con datos existentes.',
    final s? when s >= 500 => 'El servidor tuvo un problema. Inténtalo más tarde.',
    _ => 'Ocurrió un error inesperado. Inténtalo de nuevo.',
  };
}
