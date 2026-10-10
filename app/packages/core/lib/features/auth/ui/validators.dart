final _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

String? validateEmail(String? value) {
  final email = value?.trim() ?? '';
  if (email.isEmpty) return 'Introduce tu correo electrónico';
  if (!_emailPattern.hasMatch(email)) return 'Introduce un correo válido';
  return null;
}
