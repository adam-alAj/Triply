import 'package:dio/dio.dart';

import '../storage/token_storage.dart';

/// Attaches the stored JWT (if any) as a `Bearer` header on every outgoing
/// request. Requests made before login (e.g. `/api/auth/login` itself)
/// simply have no token yet and go out unauthenticated, which is correct
/// since those endpoints aren't behind `[Authorize]`.
class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokenStorage);

  final TokenStorage _tokenStorage;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await _tokenStorage.readToken();

    if (token != null && token.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $token';
    }

    handler.next(options);
  }
}
