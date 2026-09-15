import 'package:flutter/foundation.dart';

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
  }) : _repository = repository;

  final AuthRepository _repository;

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

      _user = result.user;
      _status = AuthStatus.success;
    } catch (error) {
      _errorMessage = error.toString().replaceFirst('Exception: ', '');
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

      _user = result.user;
      _status = AuthStatus.success;
    } catch (error) {
      _errorMessage = error.toString().replaceFirst('Exception: ', '');
      _status = AuthStatus.failure;
    }

    notifyListeners();
  }

  void clearError() {
    _errorMessage = null;
    if (_status == AuthStatus.failure) {
      _status = AuthStatus.idle;
    }
    notifyListeners();
  }
}