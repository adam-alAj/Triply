import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists the JWT bearer token across app restarts.
///
/// Backed by the platform keystore/keychain (via [FlutterSecureStorage]),
/// never plain SharedPreferences — the token is a credential, not a setting.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const _tokenKey = 'auth_token';

  final FlutterSecureStorage _storage;

  Future<void> saveToken(String token) =>
      _storage.write(key: _tokenKey, value: token);

  Future<String?> readToken() => _storage.read(key: _tokenKey);

  Future<void> clearToken() => _storage.delete(key: _tokenKey);
}
