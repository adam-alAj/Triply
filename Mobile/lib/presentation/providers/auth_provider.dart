import 'package:flutter/foundation.dart';

import '../../core/storage/profile_local_storage.dart';
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
    ProfileLocalStorage? profileStorage,
  })  : _repository = repository,
        _profileStorage = profileStorage ?? ProfileLocalStorage();

  final AuthRepository _repository;
  final ProfileLocalStorage _profileStorage;

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

      _user = await _withLocalNameOverride(result.user);
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

  /// Applies a locally-saved display-name edit (see [ProfileLocalStorage])
  /// on top of whatever the login response returned, since the backend
  /// doesn't persist name edits yet.
  Future<AuthUser> _withLocalNameOverride(AuthUser user) async {
    final savedName = await _profileStorage.readDisplayName(user.id);
    return savedName == null ? user : user.copyWith(name: savedName);
  }

  /// Profile screen's "Edit name" — kept on-device only (see
  /// [ProfileLocalStorage] for why) until the backend supports it.
  Future<void> updateDisplayName(String name) async {
    final user = _user;
    if (user == null || name.trim().isEmpty) return;

    _user = user.copyWith(name: name.trim());
    await _profileStorage.saveDisplayName(user.id, name.trim());
    notifyListeners();
  }

  Future<void> logout() async {
    await _repository.logout();
    _user = null;
    _status = AuthStatus.idle;
    _errorMessage = null;
    notifyListeners();
  }
}