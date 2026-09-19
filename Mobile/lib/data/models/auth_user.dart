class AuthUser {
  const AuthUser({
    required this.id,
    required this.name,
    required this.email,
  });

  final String id;
  final String name;
  final String email;

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'].toString(),
      name: json['name'] as String,
      email: json['email'] as String,
    );
  }

  /// Parses `GET/PATCH /api/users/me`'s `UserProfileResponse` shape
  /// (`{ id, email, displayName }`) — distinct from [fromJson] because the
  /// field is `displayName`, not `name`, and can be null for accounts that
  /// never set one.
  factory AuthUser.fromProfileJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'].toString(),
      name: (json['displayName'] as String?) ?? 'Traveler',
      email: json['email'] as String,
    );
  }

  AuthUser copyWith({String? name}) {
    return AuthUser(id: id, name: name ?? this.name, email: email);
  }
}