import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../cache/stale_data.dart';
import '../network/connectivity.dart';

/// Tooltip of the write actions disabled without a connection.
const needsConnectionMessage = 'Necesitas conexión';

/// "hace X" for data received at [fetchedAt].
String describeDataAge(DateTime fetchedAt, DateTime now) {
  final age = now.difference(fetchedAt);
  if (age.inMinutes < 1) return 'hace un momento';
  if (age.inHours < 1) return 'hace ${age.inMinutes} min';
  if (age.inDays < 1) return 'hace ${age.inHours} h';
  if (age.inDays == 1) return 'hace 1 día';
  return 'hace ${age.inDays} días';
}

/// Text of the [OfflineBanner].
String offlineBannerText(DateTime fetchedAt, DateTime now) =>
    'Sin conexión · datos de ${describeDataAge(fetchedAt, now)}';

/// Strip shown on top of a screen while any of its data in [scopes] comes from
/// the offline cache (see `staleSinceProvider`). Invisible otherwise.
class OfflineBanner extends ConsumerWidget {
  const OfflineBanner({super.key, required this.scopes});

  final List<StaleScope> scopes;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    DateTime? oldest;
    for (final scope in scopes) {
      final since = ref.watch(staleSinceProvider(scope));
      if (since != null && (oldest == null || since.isBefore(oldest))) oldest = since;
    }
    if (oldest == null) return const SizedBox.shrink();

    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    return Material(
      key: const Key('offline-banner'),
      color: scheme.secondaryContainer,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
        child: Row(
          children: [
            Icon(Icons.cloud_off_outlined, size: 18, color: scheme.onSecondaryContainer),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                offlineBannerText(oldest.toLocal(), DateTime.now()),
                style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSecondaryContainer),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Builds a write action with whether it can run now. While offline the result
/// gets the tooltip "Necesitas conexión"; the builder disables it (passing a
/// null callback), so it is shown greyed out.
///
/// ```dart
/// OfflineAware(
///   builder: (context, canWrite) =>
///       FilledButton(onPressed: canWrite ? _save : null, child: const Text('Guardar')),
/// )
/// ```
class OfflineAware extends ConsumerWidget {
  const OfflineAware({super.key, required this.builder});

  final Widget Function(BuildContext context, bool canWrite) builder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canWrite = ref.watch(canWriteProvider);
    final child = builder(context, canWrite);
    return canWrite ? child : Tooltip(message: needsConnectionMessage, child: child);
  }
}

/// Floating action button of a write action: greyed out and inert while
/// offline, with the tooltip "Necesitas conexión". [fabKey] goes to the inner
/// button (tests tap it).
class OfflineAwareFab extends ConsumerWidget {
  const OfflineAwareFab({
    super.key,
    this.fabKey,
    required this.onPressed,
    required this.icon,
    this.label,
    this.tooltip,
  });

  final Key? fabKey;
  final VoidCallback onPressed;
  final Widget icon;

  /// With a label it is an extended button.
  final Widget? label;
  final String? tooltip;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canWrite = ref.watch(canWriteProvider);
    final scheme = Theme.of(context).colorScheme;
    final background = canWrite ? null : scheme.onSurface.withValues(alpha: 0.12);
    final foreground = canWrite ? null : scheme.onSurface.withValues(alpha: 0.38);
    final elevation = canWrite ? null : 0.0;
    final tip = canWrite ? tooltip : needsConnectionMessage;
    final callback = canWrite ? onPressed : null;

    final label = this.label;
    if (label != null) {
      return FloatingActionButton.extended(
        key: fabKey,
        onPressed: callback,
        icon: icon,
        label: label,
        tooltip: tip,
        backgroundColor: background,
        foregroundColor: foreground,
        elevation: elevation,
      );
    }
    return FloatingActionButton(
      key: fabKey,
      onPressed: callback,
      tooltip: tip,
      backgroundColor: background,
      foregroundColor: foreground,
      elevation: elevation,
      child: icon,
    );
  }
}

/// Places an [OfflineBanner] for [scopes] above [child] (a screen body).
class OfflineBannerLayout extends StatelessWidget {
  const OfflineBannerLayout({super.key, required this.scopes, required this.child});

  final List<StaleScope> scopes;
  final Widget child;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      OfflineBanner(scopes: scopes),
      Expanded(child: child),
    ],
  );
}
