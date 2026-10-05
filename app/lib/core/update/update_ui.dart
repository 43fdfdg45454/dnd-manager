import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../server/server_config_controller.dart';
import 'app_release.dart';
import 'update_controller.dart';

/// Opens the APK download of [release] in the browser; reports failures.
Future<void> _download(BuildContext context, WidgetRef ref, AppRelease release) async {
  final messenger = ScaffoldMessenger.maybeOf(context);
  var opened = false;
  try {
    opened = await ref.read(urlOpenerProvider)(Uri.parse(release.downloadUrl));
  } catch (_) {
    opened = false;
  }
  if (!opened) {
    messenger?.showSnackBar(const SnackBar(content: Text('No se pudo abrir la descarga.')));
  }
}

String _versionLine(AppRelease release) {
  final size = formatReleaseSize(release.sizeBytes);
  return 'Versión ${release.version}${size.isEmpty ? '' : ' · $size'}';
}

class _ReleaseNotes extends StatelessWidget {
  const _ReleaseNotes({required this.release});

  final AppRelease release;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          _versionLine(release),
          key: const Key('update-version'),
          style: theme.textTheme.titleSmall,
        ),
        if (release.notes.trim().isNotEmpty) ...[
          const SizedBox(height: 12),
          Text(release.notes.trim(), key: const Key('update-notes')),
        ],
      ],
    );
  }
}

/// Offers an optional update: notes and "Descargar".
class UpdateDialog extends ConsumerWidget {
  const UpdateDialog({super.key, required this.release});

  final AppRelease release;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return AlertDialog(
      key: const Key('update-dialog'),
      title: const Text('Nueva versión disponible'),
      content: SingleChildScrollView(child: _ReleaseNotes(release: release)),
      actions: [
        TextButton(
          key: const Key('update-later'),
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Más tarde'),
        ),
        FilledButton.icon(
          key: const Key('update-download'),
          onPressed: () async {
            await _download(context, ref, release);
            if (context.mounted) Navigator.of(context).pop();
          },
          icon: const Icon(Icons.download),
          label: const Text('Descargar'),
        ),
      ],
    );
  }
}

Future<void> showUpdateDialog(BuildContext context, AppRelease release) => showDialog<void>(
  context: context,
  builder: (_) => UpdateDialog(release: release),
);

/// Shown instead of the app while a mandatory update is not installed.
class UpdateRequiredPage extends ConsumerStatefulWidget {
  const UpdateRequiredPage({super.key, required this.release});

  final AppRelease release;

  @override
  ConsumerState<UpdateRequiredPage> createState() => _UpdateRequiredPageState();
}

class _UpdateRequiredPageState extends ConsumerState<UpdateRequiredPage> {
  bool _checking = false;

  Future<void> _recheck() async {
    setState(() => _checking = true);
    final outcome = await ref.read(updateControllerProvider.notifier).check(force: true);
    if (!mounted) return;
    setState(() => _checking = false);
    if (outcome == UpdateCheckOutcome.failed) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('No se pudo comprobar si hay actualizaciones.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      key: const Key('update-required'),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Icon(Icons.system_update, size: 64, color: theme.colorScheme.primary),
                  const SizedBox(height: 16),
                  Text(
                    'Actualización obligatoria',
                    style: theme.textTheme.headlineSmall,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Instala la nueva versión para seguir usando la app.',
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 24),
                  _ReleaseNotes(release: widget.release),
                  const SizedBox(height: 24),
                  FilledButton.icon(
                    key: const Key('update-required-download'),
                    onPressed: () => _download(context, ref, widget.release),
                    icon: const Icon(Icons.download),
                    label: const Text('Descargar'),
                  ),
                  const SizedBox(height: 8),
                  TextButton(
                    key: const Key('update-required-recheck'),
                    onPressed: _checking ? null : _recheck,
                    child: const Text('Comprobar de nuevo'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Checks for updates once the server is configured (at most once a day),
/// offers optional ones in a dialog and replaces the whole app with
/// [UpdateRequiredPage] while a mandatory one is pending. [navigatorKey] is the
/// router's navigator, used to show the dialog.
class UpdateGate extends ConsumerStatefulWidget {
  const UpdateGate({super.key, required this.navigatorKey, required this.child});

  final GlobalKey<NavigatorState> navigatorKey;
  final Widget child;

  @override
  ConsumerState<UpdateGate> createState() => _UpdateGateState();
}

class _UpdateGateState extends ConsumerState<UpdateGate> {
  @override
  void initState() {
    super.initState();
    ref.listenManual<bool>(serverConfigProvider.select((config) => config.isConfigured), (
      _,
      configured,
    ) {
      // After the frame: the check changes providers and may show a dialog.
      if (configured) WidgetsBinding.instance.addPostFrameCallback((_) => _autoCheck());
    }, fireImmediately: true);
  }

  Future<void> _autoCheck() async {
    if (!mounted) return;
    final outcome = await ref.read(updateControllerProvider.notifier).check();
    if (!mounted || outcome != UpdateCheckOutcome.available) return;
    final release = ref.read(updateControllerProvider);
    final navigatorContext = widget.navigatorKey.currentContext;
    if (release == null || release.isMandatory || navigatorContext == null) return;
    if (!navigatorContext.mounted) return;
    await showUpdateDialog(navigatorContext, release);
  }

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(updateControllerProvider);
    if (pending == null || !pending.isMandatory) return widget.child;
    // The router's navigator is replaced, so the page brings its own.
    return Navigator(
      pages: [
        MaterialPage<void>(
          key: ValueKey('update-${pending.buildNumber}'),
          child: UpdateRequiredPage(release: pending),
        ),
      ],
      onDidRemovePage: (_) {},
    );
  }
}

/// Forced check from the user menu ("Buscar actualizaciones"), with feedback.
Future<void> checkForUpdatesFromMenu(BuildContext context, WidgetRef ref) async {
  final messenger = ScaffoldMessenger.of(context);
  void say(String text) => messenger.showSnackBar(SnackBar(content: Text(text)));

  final outcome = await ref.read(updateControllerProvider.notifier).check(force: true);
  if (!context.mounted) return;
  switch (outcome) {
    case UpdateCheckOutcome.available:
      final release = ref.read(updateControllerProvider);
      // A mandatory one takes over the app through the gate.
      if (release != null && !release.isMandatory) await showUpdateDialog(context, release);
    case UpdateCheckOutcome.upToDate:
      say('Tienes la última versión.');
    case UpdateCheckOutcome.skipped:
      say('No se puede comprobar la versión instalada.');
    case UpdateCheckOutcome.failed:
      say('No se pudo comprobar si hay actualizaciones.');
  }
}
