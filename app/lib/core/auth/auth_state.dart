import 'user_dto.dart';

/// Global session state: `unknown` until the stored session has been checked.
sealed class AuthState {
  const AuthState();
}

class AuthUnknown extends AuthState {
  const AuthUnknown();
}

class AuthSignedOut extends AuthState {
  const AuthSignedOut();
}

class AuthSignedIn extends AuthState {
  const AuthSignedIn(this.user, {this.isOffline = false});

  final UserDto user;

  /// The session was restored without reaching the server: [user] is the one
  /// saved on this device. It is checked again once the server answers.
  final bool isOffline;
}
