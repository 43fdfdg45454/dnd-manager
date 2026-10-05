import 'package:flutter/material.dart';

import '../../../../core/network/api_error.dart';
import '../../../campaigns/ui/feedback.dart';

/// Spanish message for a failed combat action. Network failures read
/// "Sin conexión" (nothing changed); a 400 shows the server's reason when it
/// sends one (e.g. arcane recovery over the limit).
String describeCombatError(Object error, {Map<int, String> byStatus = const {}}) {
  final base = describeCharacterError(error, byStatus: byStatus);
  if (base == networkErrorMessage) return 'Sin conexión. No se ha cambiado nada.';
  final detail = problemDetail(error);
  if (detail != null && !byStatus.containsKey(400)) return detail;
  return base;
}

/// [runAction] with the combat error texts. Returns true when the action completed.
Future<bool> runCombat(
  BuildContext context,
  Future<void> Function() action, {
  String? success,
  Map<int, String> errors = const {},
}) => runAction(context, action, success: success, errors: errors, describe: describeCombatError);

/// Shows a short message in a SnackBar.
void showCombatMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

/// Asks for a whole number; resolves to null when cancelled.
Future<int?> promptNumber(
  BuildContext context, {
  required String title,
  required String label,
  int initial = 0,
  int min = 0,
  int? max,
  String confirmLabel = 'Aceptar',
}) {
  final controller = TextEditingController(text: '$initial');
  return showDialog<int>(
    context: context,
    builder: (dialogContext) {
      String? error;
      return StatefulBuilder(
        builder: (context, setState) {
          void submit() {
            final value = int.tryParse(controller.text.trim());
            if (value == null || value < min || (max != null && value > max)) {
              setState(
                () => error = max == null
                    ? 'Introduce un número de $min en adelante.'
                    : 'Introduce un número entre $min y $max.',
              );
              return;
            }
            Navigator.of(dialogContext).pop(value);
          }

          return AlertDialog(
            title: Text(title),
            content: TextField(
              key: const Key('number-field'),
              controller: controller,
              autofocus: true,
              keyboardType: TextInputType.number,
              decoration: InputDecoration(labelText: label, errorText: error),
              onSubmitted: (_) => submit(),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                key: const Key('number-confirm'),
                onPressed: submit,
                child: Text(confirmLabel),
              ),
            ],
          );
        },
      );
    },
  );
}

/// A row of dots, [filled] of [total] filled. Tapping and long-pressing the
/// whole row (a big target for the thumb) call [onTap] and [onLongPress].
class PipRow extends StatelessWidget {
  const PipRow({
    super.key,
    required this.total,
    required this.filled,
    this.onTap,
    this.onLongPress,
    this.semanticLabel,
  });

  final int total;
  final int filled;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme.primary;
    return Semantics(
      label: semanticLabel,
      child: InkWell(
        borderRadius: BorderRadius.circular(24),
        onTap: onTap,
        onLongPress: onLongPress,
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
            child: Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 8,
              runSpacing: 8,
              children: [for (var i = 0; i < total; i++) Pip(filled: i < filled, color: color)],
            ),
          ),
        ),
      ),
    );
  }
}

/// One dot of a [PipRow].
class Pip extends StatelessWidget {
  const Pip({super.key, required this.filled, required this.color});

  final bool filled;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 24,
      height: 24,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: filled ? color : Colors.transparent,
        border: Border.all(color: color, width: 2),
      ),
    );
  }
}

/// A titled card that groups a section of the combat view.
class CombatCard extends StatelessWidget {
  const CombatCard({super.key, this.title, this.trailing, required this.child});

  final String? title;
  final Widget? trailing;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (title != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Expanded(child: Text(title!, style: theme.textTheme.titleMedium)),
                    ?trailing,
                  ],
                ),
              ),
            child,
          ],
        ),
      ),
    );
  }
}
