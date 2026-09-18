import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// PENDING BACKEND: there is no endpoint to persist a display-name edit
/// (AuthController only has register/login) — this keeps the edit on the
/// device, keyed by user id, until one exists.
class ProfileLocalStorage {
  ProfileLocalStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  String _key(String userId) => 'profile_display_name_$userId';

  Future<void> saveDisplayName(String userId, String name) =>
      _storage.write(key: _key(userId), value: name);

  Future<String?> readDisplayName(String userId) =>
      _storage.read(key: _key(userId));
}
