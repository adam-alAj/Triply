import 'dart:io' show Platform, Socket, SocketException;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

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

  /// Resolves the base URL (probing candidates on Android — see
  /// [resolveDefaultBaseUrl]) and constructs the client with it. The
  /// composition root (`main()`) awaits this once at startup instead of
  /// every caller needing to know the resolution is asynchronous.
  static Future<ApiClient> create({Dio? dio, String? baseUrl}) async {
    return ApiClient(
      dio: dio,
      baseUrl: baseUrl ?? await resolveDefaultBaseUrl(),
    );
  }

  /// Local-dev backend port. Kept as one named constant rather than typed
  /// into each candidate URL below.
  static const int _devPort = 8080;

  /// Local-dev backend URL. Android has two possible run targets that need
  /// different hosts to reach the same machine, and there's no reliable way
  /// to know which one a given run is *before* trying:
  /// - **Emulator**: reaches the host machine via the special loopback
  ///   alias `10.0.2.2` — `localhost` on an emulator means the emulator
  ///   itself.
  /// - **Real device over USB**: `10.0.2.2` doesn't exist on real hardware
  ///   at all. Reachable via `localhost` instead, once `adb reverse tcp:8080
  ///   tcp:8080` has forwarded the device's own `localhost:8080` to the
  ///   host's — run that once per USB connection (a fresh `flutter run`
  ///   after reconnecting the cable is a common time to re-run it, since
  ///   the forward doesn't survive a device disconnect).
  ///
  /// Rather than hardcode one and require editing this file to run on the
  /// other target, this probes both with a short timeout and caches
  /// whichever answers first — same app build works unmodified on either.
  /// iOS simulator / desktop / web reach the host directly via `localhost`,
  /// so they skip probing entirely. A physical device reached over Wi-Fi
  /// instead of USB (no adb bridge available) needs an explicit `baseUrl`
  /// passed to the constructor instead — neither candidate here can reach
  /// it.
  static Future<String> resolveDefaultBaseUrl() async {
    if (kIsWeb || !Platform.isAndroid) {
      return 'http://localhost:$_devPort';
    }

    const candidateHosts = ['10.0.2.2', 'localhost'];
    for (final host in candidateHosts) {
      if (await _canReach(host, _devPort)) {
        return 'http://$host:$_devPort';
      }
    }

    // Neither answered (e.g. backend isn't running yet) — fall back to the
    // emulator address so behavior matches what this used to always return.
    return 'http://${candidateHosts.first}:$_devPort';
  }

  static Future<bool> _canReach(String host, int port) async {
    try {
      final socket = await Socket.connect(
        host,
        port,
        timeout: const Duration(milliseconds: 800),
      );
      socket.destroy();
      return true;
    } on SocketException {
      return false;
    }
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
