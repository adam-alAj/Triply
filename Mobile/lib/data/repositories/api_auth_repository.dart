import '../../core/network/api_client.dart';
import '../../core/storage/token_storage.dart';
import '../models/auth_user.dart';
import 'auth_repository.dart';

/// Real backend-backed implementation of [AuthRepository], calling
/// `POST /api/auth/login` and `POST /api/auth/register` (see
/// Backend/Triply.Api/Modules/Auth/AuthController.cs).
///
/// NOTE: the backend's `AuthResponse` currently returns
/// `{ token, expiresAtUtc, userId, email }` — no display name. Registration
/// keeps the name the user just typed; login falls back to the email's local
/// part until the backend adds `DisplayName` to the response (or a
/// `GET /api/users/me` endpoint) — flagged to the backend track separately.
class ApiAuthRepository implements AuthRepository {
  ApiAuthRepository({
    required ApiClient apiClient,
    required TokenStorage tokenStorage,
  })  : _apiClient = apiClient,
        _tokenStorage = tokenStorage;

  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;

  @override
  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/auth/login',
      data: {
        'email': email,
        'password': password,
      },
    );

    return _handleAuthResponse(
      response,
      fallbackName: _nameFromEmail(email),
    );
  }

  @override
  Future<AuthResult> register({
    required String name,
    required String email,
    required String password,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/auth/register',
      data: {
        'email': email,
        'password': password,
        'displayName': name,
      },
    );

    return _handleAuthResponse(response, fallbackName: name);
  }

  Future<AuthResult> _handleAuthResponse(
    Map<String, dynamic> json, {
    required String fallbackName,
  }) async {
    final token = json['token'] as String;
    final userId = json['userId'].toString();
    final email = json['email'] as String;

    await _tokenStorage.saveToken(token);

    return AuthResult(
      user: AuthUser(id: userId, name: fallbackName, email: email),
      token: token,
    );
  }

  String _nameFromEmail(String email) {
    final atIndex = email.indexOf('@');
    return atIndex > 0 ? email.substring(0, atIndex) : email;
  }
}
