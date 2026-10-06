import 'package:flutter/widgets.dart';

import '../../../../core/motion/rest_animations.dart';
import '../../data/models.dart' show RestKind;

/// Plays the animation of an approved rest over the whole screen: a campfire
/// ([CampfireBurst]) for a short rest, the moon crossing the night
/// ([MoonPass]) for a long one. The overlay never takes touches and removes
/// itself when the animation ends (at once under reduced motion).
void showRestCelebration(BuildContext context, RestKind kind) {
  final overlay = Overlay.maybeOf(context, rootOverlay: true);
  if (overlay == null) return;
  late final OverlayEntry entry;
  var removed = false;
  void remove() {
    if (removed) return;
    removed = true;
    entry.remove();
  }

  entry = OverlayEntry(
    builder: (_) => RestCelebration(kind: kind, onDone: remove),
  );
  overlay.insert(entry);
}

/// The full-screen layer of [showRestCelebration]: plays once when mounted
/// and calls [onDone] at the end.
class RestCelebration extends StatelessWidget {
  const RestCelebration({super.key, required this.kind, this.onDone});

  final RestKind kind;
  final VoidCallback? onDone;

  @override
  Widget build(BuildContext context) {
    const area = SizedBox.expand();
    return IgnorePointer(
      child: switch (kind) {
        RestKind.short => CampfireBurst(
          key: const Key('rest-celebration-short'),
          playOnMount: true,
          onCompleted: onDone,
          child: area,
        ),
        RestKind.long => MoonPass(
          key: const Key('rest-celebration-long'),
          playOnMount: true,
          onCompleted: onDone,
          child: area,
        ),
      },
    );
  }
}
