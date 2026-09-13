import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

/// Thin wrapper around [Dio] that owns every piece of HTTP configuration in
/// the app: base URL, timeouts, headers, interceptors and error translation.
///
/// This is the only class allowed to touch Dio directly. Repositories depend
/// on this class; the presentation layer never does.
class ApiClient {
  ApiClient({Dio? dio, String baseUrl = defaultBaseUrl}) : _dio = dio ?? Dio() {
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

  /// Placeholder base URL. Swap for the Triply backend once it exists.
  static const String defaultBaseUrl = 'https://jsonplaceholder.typicode.com';

  final Dio _dio;

  /// Exposed so future interceptors (auth token, retry) can be registered by
  /// the composition root without reaching for a new Dio instance.
  Interceptors get interceptors => _dio.interceptors;

  Future<T> get<T>(String path, {Map<String, dynamic>? queryParameters}) {
    return _send<T>(() => _dio.get<T>(path, queryParameters: queryParameters));
  }

  Future<T> post<T>(String path, {Object? data}) {
    return _send<T>(() => _dio.post<T>(path, data: data));
  }

  Future<T> put<T>(String path, {Object? data}) {
    return _send<T>(() => _dio.put<T>(path, data: data));
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
        'The server responded with status ${statusCode ?? 'unknown'}.',
      DioExceptionType.unknown => error.message ?? 'An unknown error occurred.',
    };
    return ApiException(message, statusCode: statusCode);
  }

  final String message;
  final int? statusCode;

  @override
  String toString() => 'ApiException(${statusCode ?? '-'}): $message';
}
