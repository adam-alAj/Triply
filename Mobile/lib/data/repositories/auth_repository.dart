import '../models/auth_user.dart';

class AuthResult {
  const AuthResult({
    required this.user,
    required this.token,
  });

  final AuthUser user;
  final String token;
}

abstract class AuthRepository {
  Future<AuthResult> login({
    required String email,
    required String password,
  });

  Future<AuthResult> register({
    required String name,
    required String email,
    required String password,
  });
}