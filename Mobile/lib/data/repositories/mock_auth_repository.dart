import '../models/auth_user.dart';
import 'auth_repository.dart';

class MockAuthRepository implements AuthRepository {
  @override
  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    await Future.delayed(const Duration(milliseconds: 800));

    if (email.isEmpty || password.isEmpty) {
      throw Exception('Please enter your email and password.');
    }

    return AuthResult(
      user: AuthUser(
        id: 'mock-user-1',
        name: 'Alex Traveler',
        email: email,
      ),
      token: 'mock-jwt-token',
    );
  }

  @override
  Future<AuthResult> register({
    required String name,
    required String email,
    required String password,
  }) async {
    await Future.delayed(const Duration(milliseconds: 800));

    if (name.isEmpty || email.isEmpty || password.isEmpty) {
      throw Exception('Please complete all required fields.');
    }

    return AuthResult(
      user: AuthUser(
        id: 'mock-user-2',
        name: name,
        email: email,
      ),
      token: 'mock-jwt-token',
    );
  }

  @override
  Future<void> logout() async {}
}