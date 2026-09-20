import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../data/models/auth_user.dart';
import '../../data/repositories/auth_repository.dart';

enum AuthStatus {
  idle,
  loading,
  success,
  failure,
}

class AuthProvider extends ChangeNotifier {
  AuthProvider({
    required AuthRepository repository,
    required ApiClient apiClient,
  })  : _repository = repository,
        _apiClient = apiClient;

  final AuthRepository _repository;
  final ApiClient _apiClient;

  AuthStatus _status = AuthStatus.idle;
  AuthUser? _user;
  String? _errorMessage;

  AuthStatus get status => _status;
  AuthUser? get user => _user;
  String? get errorMessage => _errorMessage;

  bool get isLoading => _status == AuthStatus.loading;

  Future<void> login({
    required String email,
    required String password,
  }) async {
    if (isLoading) return;

    _status = AuthStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final result = await _repository.login(
        email: email,
        password: password,
      );

      _user = await _fetchProfileOr(result.user);
      _status = AuthStatus.success;
    } catch (error) {
      _errorMessage = _cleanMessage(error);
      _status = AuthStatus.failure;
    }

    notifyListeners();
  }

  Future<void> register({
    required String name,
    required String email,
    required String password,
  }) async {
    if (isLoading) return;

    _status = AuthStatus.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final result = await _repository.register(
        name: name,
        email: email,
        password: password,
      );

      _user = await _fetchProfileOr(result.user);
      _status = AuthStatus.success;
    } catch (error) {
      _errorMessage = _cleanMessage(error);
      _status = AuthStatus.failure;
    }

    notifyListeners();
  }

  /// Never show a raw exception to the user (08_SYSTEM_DESIGN.md §36: no
  /// stack traces / API internals). [ApiException] already carries a clean
  /// message; [MockAuthRepository] throws plain `Exception('...')`, whose
  /// `toString()` is prefixed with "Exception: " — strip that. Anything
  /// else falls back to a generic message rather than leaking `toString()`.
  String _cleanMessage(Object error) {
    if (error is ApiException) return error.message;

    final text = error.toString();
    const prefix = 'Exception: ';
    if (text.startsWith(prefix)) return text.substring(prefix.length);

    return 'Something went wrong. Please try again.';
  }

  void clearError() {
    _errorMessage = null;
    if (_status == AuthStatus.failure) {
      _status = AuthStatus.idle;
    }
    notifyListeners();
  }

  /// `GET /api/auth/login|register` doesn't return a display name (only
  /// `{ token, expiresAtUtc, userId, email }`), so the freshly-logged-in
  /// user is a guess (email local-part, or whatever was just typed at
  /// registration). Fetching `GET /api/users/me` right after gets the real,
  /// previously-saved name; if that call fails for any reason, the guess
  /// from [fallback] is still good enough to not block login.
  Future<AuthUser> _fetchProfileOr(AuthUser fallback) async {
    try {
      final json = await _apiClient.get<Map<String, dynamic>>('/api/users/me');
      return AuthUser.fromProfileJson(json);
    } catch (_) {
      return fallback;
    }
  }

  /// Session restore (splash screen): a saved token means the user already
  /// logged in on this device — fetch their real profile via
  /// `GET /api/users/me` rather than just trusting the token exists.
  /// Returns whether the session is actually valid. Only a genuine auth
  /// rejection (401/403 — the token is actually invalid) clears it out via
  /// [logout] so the caller can route to onboarding instead of a broken
  /// Home. Any other failure (no network, backend unreachable, timeout) is
  /// transient and must NOT wipe a token that might still be good —
  /// otherwise a dropped connection on launch permanently signs the user
  /// out.
  Future<bool> restoreSession() async {
    try {
      final json = await _apiClient.get<Map<String, dynamic>>('/api/users/me');
      _user = AuthUser.fromProfileJson(json);
      _status = AuthStatus.success;
      notifyListeners();
      return true;
    } catch (error) {
      final isAuthRejection = error is ApiException &&
          (error.statusCode == 401 || error.statusCode == 403);
      if (isAuthRejection) {
        await logout();
      }
      return false;
    }
  }

  /// Profile screen's "Edit name" — `PATCH /api/users/me`.
  Future<bool> updateDisplayName(String name) async {
    final user = _user;
    final trimmed = name.trim();
    if (user == null || trimmed.isEmpty) return false;

    try {
      final json = await _apiClient.patch<Map<String, dynamic>>(
        '/api/users/me',
        data: {'displayName': trimmed},
      );
      _user = AuthUser.fromProfileJson(json);
      notifyListeners();
      return true;
    } catch (error) {
      _errorMessage = _cleanMessage(error);
      notifyListeners();
      return false;
    }
  }

  Future<void> logout() async {
    await _repository.logout();
    _user = null;
    _status = AuthStatus.idle;
    _errorMessage = null;
    notifyListeners();
  }
}