import 'package:flutter/widgets.dart';

import 'motion_settings.dart';

/// Base of the animations that play once per event (a hit, a heal, a smite, a
/// rest): they play every time [trigger] changes to a new non-null value, or
/// once when mounted if [playOnMount] is set.
///
/// With [enabled] false, or under reduced motion ([MotionScope]), they do not
/// animate: they jump to their end state, which for every one-shot is "nothing
/// drawn". [onCompleted] is called after each play, in the next frame.
abstract class OneShotMotion extends StatefulWidget {
  const OneShotMotion({
    super.key,
    this.trigger,
    this.enabled = true,
    this.playOnMount = false,
    this.onCompleted,
  });

  /// Any value that changes when the animation must play again (a counter, a
  /// timestamp, the id of the event…). Null never plays.
  final Object? trigger;

  final bool enabled;

  /// Plays once when first built.
  final bool playOnMount;

  final VoidCallback? onCompleted;
}

/// State of a [OneShotMotion]: owns the [controller] of [duration] and plays it
/// when the trigger changes.
abstract class OneShotMotionState<T extends OneShotMotion> extends State<T>
    with SingleTickerProviderStateMixin {
  /// Length of one play.
  Duration get duration;

  late final AnimationController controller = AnimationController(vsync: this, duration: duration);

  bool _first = true;

  /// Whether the animation is currently running (between start and end state).
  bool get playing => controller.isAnimating;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_first) {
      _first = false;
      if (widget.playOnMount) play();
    }
  }

  @override
  void didUpdateWidget(T oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.trigger != null && widget.trigger != oldWidget.trigger) play();
  }

  /// Plays the animation from the start, or jumps to the end state when
  /// motion is disabled or reduced.
  void play() {
    if (!widget.enabled || MotionScope.reducedOf(context)) {
      controller.value = 1;
      _completed();
      return;
    }
    controller.forward(from: 0).then((_) => _completed(), onError: (_) {});
  }

  void _completed() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) widget.onCompleted?.call();
    });
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }
}
