import 'package:flutter/material.dart';

/// Asks how many units (1..[max]) of something to move, starting at [initial]
/// (the maximum by default). Pops with the quantity, or null when cancelled.
/// With [max] 1 it is a plain confirmation.
Future<int?> showQuantityDialog(
  BuildContext context, {
  required String title,
  required String message,
  required int max,
  required String confirmLabel,
  int? initial,
}) => showDialog<int>(
  context: context,
  builder: (_) => _QuantityDialog(
    title: title,
    message: message,
    max: max < 1 ? 1 : max,
    initial: initial,
    confirmLabel: confirmLabel,
  ),
);

class _QuantityDialog extends StatefulWidget {
  const _QuantityDialog({
    required this.title,
    required this.message,
    required this.max,
    required this.confirmLabel,
    this.initial,
  });

  final String title;
  final String message;
  final int max;
  final int? initial;
  final String confirmLabel;

  @override
  State<_QuantityDialog> createState() => _QuantityDialogState();
}

class _QuantityDialogState extends State<_QuantityDialog> {
  late int _quantity = (widget.initial ?? widget.max).clamp(1, widget.max);

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(widget.message),
          if (widget.max > 1) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                const Text('Cantidad'),
                const Spacer(),
                IconButton(
                  key: const Key('quantity-minus'),
                  tooltip: 'Menos',
                  onPressed: _quantity > 1 ? () => setState(() => _quantity--) : null,
                  icon: const Icon(Icons.remove_circle_outline),
                ),
                Text('$_quantity', key: const Key('quantity-value')),
                IconButton(
                  key: const Key('quantity-plus'),
                  tooltip: 'Más',
                  onPressed: _quantity < widget.max ? () => setState(() => _quantity++) : null,
                  icon: const Icon(Icons.add_circle_outline),
                ),
              ],
            ),
          ],
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        FilledButton(
          key: const Key('quantity-confirm'),
          onPressed: () => Navigator.of(context).pop(_quantity),
          child: Text(widget.confirmLabel),
        ),
      ],
    );
  }
}
