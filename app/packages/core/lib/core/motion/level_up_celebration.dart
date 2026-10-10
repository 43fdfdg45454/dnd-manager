import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import '../theme/typography.dart';
import 'motion_settings.dart';

/// Full-screen celebration of a new level: runes rise from the bottom while
/// the [level] number grows with a bounce, in [duration]. Under reduced motion
/// (or with [enabled] false) it shows the end state at once. Tapping anywhere
/// or "Continuar" calls [onDismiss].
class LevelUpCelebration extends StatefulWidget {
  const LevelUpCelebration({
    super.key,
    required this.level,
    required this.onDismiss,
    this.enabled = true,
    this.title = '¡Subes de nivel!',
  });

  static const duration = Duration(milliseconds: 1800);

  final int level;
  final VoidCallback onDismiss;
  final bool enabled;
  final String title;

  @override
  State<LevelUpCelebration> createState() => _LevelUpCelebrationState();
}

class _LevelUpCelebrationState extends State<LevelUpCelebration>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: LevelUpCelebration.duration,
  );
  bool _started = false;

  static final _numberScale = CurveTween(curve: const Interval(0.1, 0.7, curve: Curves.elasticOut));
  static final _numberFade = CurveTween(curve: const Interval(0.1, 0.3, curve: Curves.easeOut));
  static final _footerFade = CurveTween(curve: const Interval(0.55, 0.9, curve: Curves.easeOut));

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_started) return;
    _started = true;
    if (!widget.enabled || MotionScope.reducedOf(context)) {
      _controller.value = 1;
    } else {
      _controller.forward();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    return Material(
      key: const Key('level-up-celebration'),
      color: const Color(0xFF000000).withValues(alpha: 0.8),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: widget.onDismiss,
        child: Stack(
          children: [
            Positioned.fill(
              child: CustomPaint(
                painter: RisingRunesPainter(progress: _controller, color: tokens.oldGold),
              ),
            ),
            Center(
              child: AnimatedBuilder(
                animation: _controller,
                builder: (context, _) {
                  final t = _controller.value;
                  return Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Opacity(
                        opacity: _numberFade.transform(t),
                        child: Text(
                          widget.title,
                          textAlign: TextAlign.center,
                          style: theme.textTheme.headlineSmall?.copyWith(color: tokens.bone),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Transform.scale(
                        scale: math.max(0, _numberScale.transform(t)),
                        child: Text(
                          '${widget.level}',
                          key: const Key('level-up-number'),
                          style: theme.textTheme.displayLarge?.copyWith(
                            fontSize: 96,
                            fontWeight: FontWeight.w700,
                            color: tokens.oldGold,
                            fontFeatures: AppTypography.numeric.fontFeatures,
                          ),
                        ),
                      ),
                      const SizedBox(height: 24),
                      Opacity(
                        opacity: _footerFade.transform(t),
                        child: FilledButton(
                          key: const Key('level-up-dismiss'),
                          onPressed: widget.onDismiss,
                          child: const Text('Continuar'),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Shows a [LevelUpCelebration] over the whole app until it is dismissed.
Future<void> showLevelUpCelebration(BuildContext context, {required int level}) {
  final reduced = MotionScope.reducedOf(context);
  return showGeneralDialog<void>(
    context: context,
    barrierDismissible: false,
    barrierColor: const Color(0x00000000),
    transitionDuration: reduced ? Duration.zero : const Duration(milliseconds: 200),
    pageBuilder: (dialogContext, _, _) =>
        LevelUpCelebration(level: level, onDismiss: () => Navigator.of(dialogContext).pop()),
    transitionBuilder: (context, animation, _, child) =>
        FadeTransition(opacity: animation, child: child),
  );
}

/// Runes rising from the bottom of the screen and fading out, for a
/// [LevelUpCelebration]. Every rune is a few straight strokes, chosen and
/// placed with a fixed seed.
class RisingRunesPainter extends CustomPainter {
  RisingRunesPainter({required this.progress, required this.color}) : super(repaint: progress);

  final Animation<double> progress;
  final Color color;

  /// Stroke sets of the runes, on a 0..1 square.
  static const _glyphs = <List<(Offset, Offset)>>[
    [
      (Offset(0.5, 0), Offset(0.5, 1)),
      (Offset(0.5, 0.45), Offset(0.15, 0.1)),
      (Offset(0.5, 0.45), Offset(0.85, 0.1)),
    ],
    [
      (Offset(0.3, 0), Offset(0.3, 1)),
      (Offset(0.3, 0.2), Offset(0.75, 0.5)),
      (Offset(0.75, 0.5), Offset(0.3, 0.8)),
    ],
    [
      (Offset(0.2, 0), Offset(0.2, 1)),
      (Offset(0.8, 0), Offset(0.8, 1)),
      (Offset(0.2, 0.3), Offset(0.8, 0.7)),
    ],
    [
      (Offset(0.5, 0), Offset(0.15, 0.5)),
      (Offset(0.15, 0.5), Offset(0.5, 1)),
      (Offset(0.5, 1), Offset(0.85, 0.5)),
      (Offset(0.85, 0.5), Offset(0.5, 0)),
    ],
    [
      (Offset(0.3, 0), Offset(0.3, 1)),
      (Offset(0.3, 0), Offset(0.8, 0.3)),
      (Offset(0.3, 0.35), Offset(0.8, 0.65)),
    ],
  ];

  static final _runes = List.generate(16, (i) {
    final random = math.Random(300 + i);
    return (
      x: random.nextDouble(),
      glyph: random.nextInt(_glyphs.length),
      size: 14 + random.nextDouble() * 16,
      speed: 0.6 + random.nextDouble() * 0.5,
      delay: random.nextDouble() * 0.35,
    );
  });

  @override
  void paint(Canvas canvas, Size size) {
    final t = progress.value;
    if (t <= 0 || t >= 1 || size.isEmpty) return;
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 2;
    for (final rune in _runes) {
      final local = ((t - rune.delay) / (1 - rune.delay)).clamp(0.0, 1.0);
      if (local == 0 || local == 1) continue;
      final y = size.height + rune.size - local * (size.height + rune.size * 2) * rune.speed;
      final origin = Offset(rune.x * (size.width - rune.size), y);
      paint.color = color.withValues(alpha: math.sin(math.pi * local) * 0.9);
      for (final (from, to) in _glyphs[rune.glyph]) {
        canvas.drawLine(origin + from * rune.size, origin + to * rune.size, paint);
      }
    }
  }

  @override
  bool shouldRepaint(RisingRunesPainter old) => old.progress != progress || old.color != color;
}
