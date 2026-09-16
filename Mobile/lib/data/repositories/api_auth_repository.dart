import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../core/network/api_client.dart';
import '../models/auth_user.dart';
import 'auth_repository.dart';

class ApiAuthRepository implements AuthRepository {
  ApiAuthRepository({
    required ApiClient apiClient,
    FlutterSecureStorage? secureStorage,
  })  : _apiClient = apiClient,
        _secureStorage = secureStorage ?? const FlutterSecureStorage();

  final ApiClient _apiClient;
  final FlutterSecureStorage _secureStorage;

  static const String _tokenKey = 'auth_token';
  static const String _expiresAtKey = 'auth_expires_at';

  @override
  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/api/auth/login',
        data: {
          'email': email,
          'password': password,
        },
      );

      return _handleAuthResponse(
        response,
        name: '',
      );
    } on ApiException catch (error) {
      throw _mapAuthError(error);
    }
  }

  @override
  Future<AuthResult> register({
    required String name,
    required String email,
    required String password,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/api/auth/register',
        data: {
          'email': email,
          'password': password,
          'displayName': name,
        },
      );

      return _handleAuthResponse(
        response,
        name: name,
      );
    } on ApiException catch (error) {
      throw _mapAuthError(error);
    }
  }

  Future<AuthResult> _handleAuthResponse(
      Map<String, dynamic> response, {
        required String name,
      }) async {
    final token = response['token'] as String?;
    final expiresAtUtc = response['expiresAtUtc'] as String?;
    final userId = response['userId'] as String?;
    final email = response['email'] as String?;

    if (token == null ||
        expiresAtUtc == null ||
        userId == null ||
        email == null) {
      throw ApiException(
        'The server returned an invalid authentication response.',
      );
    }

    await _secureStorage.write(
      key: _tokenKey,
      value: token,
    );

    await _secureStorage.write(
      key: _expiresAtKey,
      value: expiresAtUtc,
    );

    return AuthResult(
      user: AuthUser(
        id: userId,
        name: name,
        email: email,
      ),
      token: token,
    );
  }

  ApiException _mapAuthError(ApiException error) {
    switch (error.statusCode) {
      case 400:
        return ApiException(
          'Please check your registration information.',
          statusCode: error.statusCode,
        );

      case 401:
        return ApiException(
          'Invalid email or password.',
          statusCode: error.statusCode,
        );

      case 429:
        return ApiException(
          'Too many attempts. Please try again in a moment.',
          statusCode: error.statusCode,
        );

      default:
        return error;
    }
  }

  Future<String?> getStoredToken() {
    return _secureStorage.read(key: _tokenKey);
  }

  Future<String?> getStoredExpiresAt() {
    return _secureStorage.read(key: _expiresAtKey);
  }

  Future<void> clearStoredToken() async {
    await _secureStorage.delete(key: _tokenKey);
    await _secureStorage.delete(key: _expiresAtKey);
  }
}