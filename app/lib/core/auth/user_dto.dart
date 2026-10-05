/// Global role of a user, as sent by the API (`"Admin"` or `"User"`).
enum UserRole {
  admin('Admin', 'Administrador'),
  user('User', 'Usuario');

  const UserRole(this.apiValue, this.label);

  /// Value used on the wire.
  final String apiValue;

  /// Spanish label shown in the UI.
  final String label;

  static UserRole fromApi(String value) =>
      UserRole.values.firstWhere((r) => r.apiValue == value, orElse: () => UserRole.user);
}

class UserDto {
  const UserDto({
    required this.id,
    required this.email,
    required this.displayName,
    required this.role,
    required this.isActive,
    required this.hasPassword,
    required this.createdAt,
    this.lastLoginAt,
  });

  factory UserDto.fromJson(Map<String, dynamic> json) => UserDto(
    id: json['id'] as String,
    email: json['email'] as String,
    displayName: json['displayName'] as String,
    role: UserRole.fromApi(json['role'] as String),
    isActive: json['isActive'] as bool,
    hasPassword: json['hasPassword'] as bool,
    createdAt: DateTime.parse(json['createdAt'] as String),
    lastLoginAt: json['lastLoginAt'] == null ? null : DateTime.parse(json['lastLoginAt'] as String),
  );

  final String id;
  final String email;
  final String displayName;
  final UserRole role;
  final bool isActive;
  final bool hasPassword;
  final DateTime createdAt;
  final DateTime? lastLoginAt;

  bool get isAdmin => role == UserRole.admin;

  Map<String, dynamic> toJson() => {
    'id': id,
    'email': email,
    'displayName': displayName,
    'role': role.apiValue,
    'isActive': isActive,
    'hasPassword': hasPassword,
    'createdAt': createdAt.toIso8601String(),
    'lastLoginAt': lastLoginAt?.toIso8601String(),
  };

  UserDto copyWith({String? displayName, UserRole? role, bool? isActive}) => UserDto(
    id: id,
    email: email,
    displayName: displayName ?? this.displayName,
    role: role ?? this.role,
    isActive: isActive ?? this.isActive,
    hasPassword: hasPassword,
    createdAt: createdAt,
    lastLoginAt: lastLoginAt,
  );
}
