import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import 'api_config.dart';

/// Thin wrapper around [Dio] that owns every piece of HTTP configuration in
/// the app: base URL, timeouts, headers, interceptors and error translation.
///
/// This is the only class allowed to touch Dio directly. Repositories depend
/// on this class; the presentation layer never does.
class ApiClient {
  ApiClient({Dio? dio, required String baseUrl}) : _dio = dio ?? Dio() {
    _dio.options = _dio.options.copyWith(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      sendTimeout: const Duration(seconds: 15),
      responseType: ResponseType.json,
      headers: const {'Accept': 'application/json'},
    );

    if (kDebugMode) {
      _dio.interceptors.add(
        LogInterceptor(
          requestBody: true,
          requestHeader: false,
          responseHeader: false,
          responseBody: false,
          logPrint: (Object? object) => debugPrint(object.toString()),
        ),
      );
    }
  }

  /// Resolves the base URL — see [ApiConfig] for the full emulator/real-
  /// device/simulator/override resolution logic — and constructs the
  /// client with it. The composition root (`main()`) awaits this once at
  /// startup instead of every caller needing to know the resolution is
  /// asynchronous.
  static Future<ApiClient> create({Dio? dio, String? baseUrl}) async {
    return ApiClient(
      dio: dio,
      baseUrl: baseUrl ?? await ApiConfig.resolve(),
    );
  }

  final Dio _dio;

  /// Exposed so future interceptors (auth token, retry) can be registered by
  /// the composition root without reaching for a new Dio instance.
  Interceptors get interceptors => _dio.interceptors;

  Future<T> get<T>(String path, {Map<String, dynamic>? queryParameters}) {
    return _send<T>(() => _dio.get<T>(path, queryParameters: queryParameters));
  }

  /// [receiveTimeout] overrides the default 15s for calls known to run
  /// long server-side (AI generation can take multiple bounded-retry
  /// attempts at up to `Gemini:TimeoutSeconds` each) — the default stays
  /// tight for every other call so a genuinely hung request still fails
  /// fast.
  Future<T> post<T>(String path, {Object? data, Duration? receiveTimeout}) {
    return _send<T>(
      () => _dio.post<T>(
        path,
        data: data,
        options: receiveTimeout == null
            ? null
            : Options(receiveTimeout: receiveTimeout),
      ),
    );
  }

  Future<T> put<T>(String path, {Object? data}) {
    return _send<T>(() => _dio.put<T>(path, data: data));
  }

  Future<T> patch<T>(String path, {Object? data}) {
    return _send<T>(() => _dio.patch<T>(path, data: data));
  }

  Future<T> delete<T>(String path, {Object? data}) {
    return _send<T>(() => _dio.delete<T>(path, data: data));
  }

  Future<T> _send<T>(Future<Response<T>> Function() request) async {
    try {
      final response = await request();
      final data = response.data;
      if (data == null) {
        throw ApiException(
          'The server returned an empty response.',
          statusCode: response.statusCode,
        );
      }
      return data;
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  void close() => _dio.close();
}

/// Transport-level failure. Repositories let this bubble up; providers turn it
/// into UI state. Carries no business meaning by design.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});

  factory ApiException.fromDioException(DioException error) {
    final statusCode = error.response?.statusCode;
    final message = switch (error.type) {
      DioExceptionType.connectionTimeout ||
      DioExceptionType.sendTimeout ||
      DioExceptionType.receiveTimeout ||
      DioExceptionType.transformTimeout => 'The request timed out.',
      DioExceptionType.connectionError =>
        'Could not reach the server. Check your connection.',
      DioExceptionType.cancel => 'The request was cancelled.',
      DioExceptionType.badCertificate => 'The server certificate is invalid.',
      DioExceptionType.badResponse =>
        _extractServerMessage(error) ??
            'The server responded with status ${statusCode ?? 'unknown'}.',
      DioExceptionType.unknown => error.message ?? 'An unknown error occurred.',
    };
    return ApiException(message, statusCode: statusCode);
  }

  /// Backend errors follow ASP.NET Core's `ProblemDetails` shape: a plain
  /// `{ title }` for generic failures, or `{ title, errors: { Field: [...] } }`
  /// for FluentValidation/ModelState failures. Field-level messages are more
  /// useful to show than the generic title, so they're preferred when present.
  static String? _extractServerMessage(DioException error) {
    final data = error.response?.data;
    if (data is! Map) return null;

    final errors = data['errors'];
    if (errors is Map && errors.isNotEmpty) {
      final firstValue = errors.values.first;
      if (firstValue is List && firstValue.isNotEmpty) {
        return firstValue.first.toString();
      }
    }

    final title = data['title'] ?? data['message'];
    if (title is String && title.isNotEmpty) return title;

    return null;
  }

  final String message;
  final int? statusCode;

  @override
  String toString() => 'ApiException(${statusCode ?? '-'}): $message';
}
