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
    403 => 'No tienes permiso para realizar esta acción.',
    404 => 'No se encontró el recurso solicitado.',
    409 => 'La operación entra en conflicto con datos existentes.',
    final s? when s >= 500 => 'El servidor tuvo un problema. Inténtalo más tarde.',
    _ => 'Ocurrió un error inesperado. Inténtalo de nuevo.',
  };
}

/// Status-specific messages for the campaign endpoints.
const campaignErrorMessages = <int, String>{
  400: 'La operación no es válida para este miembro.',
  403: 'No tienes permiso para hacer eso en esta campaña.',
  404: 'La campaña o el usuario no existe, o no tienes acceso.',
  409: 'Ya es miembro de esta campaña.',
};

/// Like [describeApiError] but with the campaign-specific texts. [byStatus]
/// takes precedence over [campaignErrorMessages].
String describeCampaignError(Object error, {Map<int, String> byStatus = const {}}) =>
    describeApiError(error, byStatus: {...campaignErrorMessages, ...byStatus});

/// Status-specific messages for the character and change-request endpoints.
const characterErrorMessages = <int, String>{
  400: 'Los datos enviados no son válidos para este personaje.',
  403: 'No tienes permiso para hacer eso con este personaje.',
  404: 'El personaje o la solicitud no existe, o no tienes acceso.',
  409: 'La solicitud ya no está pendiente.',
};

/// Like [describeApiError] but with the character-specific texts. [byStatus]
/// takes precedence over [characterErrorMessages].
String describeCharacterError(Object error, {Map<int, String> byStatus = const {}}) =>
    describeApiError(error, byStatus: {...characterErrorMessages, ...byStatus});

/// The `detail` of a ProblemDetails response body, when the server sent one.
String? problemDetail(Object error) {
  if (error is! DioException) return null;
  final data = error.response?.data;
  if (data is! Map) return null;
  final detail = data['detail'];
  return detail is String && detail.trim().isNotEmpty ? detail.trim() : null;
}

/// Status-specific messages for the items, inventory and shop endpoints.
const itemErrorMessages = <int, String>{
  400: 'La operación no es válida para este objeto.',
  403: 'No tienes permiso para hacer eso.',
  404: 'El objeto, la tienda o el personaje no existe, o no tienes acceso.',
  409: 'La operación entra en conflicto con datos existentes.',
};

/// Like [describeApiError] but with the items and shops texts. A 400 shows the
/// server's ProblemDetails `detail` when it has one ("No tienes suficiente
/// dinero"); otherwise [byStatus] and [itemErrorMessages] apply.
String describeItemError(Object error, {Map<int, String> byStatus = const {}}) {
  if (error is DioException && error.response?.statusCode == 400) {
    final detail = problemDetail(error);
    if (detail != null) return detail;
  }
  return describeApiError(error, byStatus: {...itemErrorMessages, ...byStatus});
}
