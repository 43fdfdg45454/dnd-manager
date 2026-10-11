import 'package:flutter/material.dart';

/// How long an in-app message (SnackBar) stays on screen.
const appMessageDuration = Duration(seconds: 3);

/// Shows [message] at the bottom of the screen for [appMessageDuration],
/// replacing any message still visible. [icon] is drawn before the text and
/// [action] at the end. Does nothing when there is no ScaffoldMessenger above
/// [context].
void showAppMessage(
  BuildContext context,
  String message, {
  SnackBarAction? action,
  Widget? icon,
  Key? key,
}) {
  final messenger = ScaffoldMessenger.maybeOf(context);
  if (messenger == null) return;
  showAppMessageWith(messenger, message, action: action, icon: icon, key: key);
}

/// [showAppMessage] for code that resolved the [messenger] up front (the
/// context may be gone once an asynchronous action finishes).
void showAppMessageWith(
  ScaffoldMessengerState messenger,
  String message, {
  SnackBarAction? action,
  Widget? icon,
  Key? key,
}) {
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        key: key,
        duration: appMessageDuration,
        content: icon == null
            ? Text(message)
            : Row(
                children: [
                  icon,
                  const SizedBox(width: 10),
                  Expanded(child: Text(message)),
                ],
              ),
        action: action,
      ),
    );
}
