import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/trust_store.dart';
import '../../../core/realtime/connection_diagnostics.dart';
import '../../../core/router/app_router.dart';
import '../../../core/server/server_config_controller.dart';
import '../../../core/server/server_probe.dart';
import '../../../core/server/server_url.dart';

/// Lets the user choose the server the app talks to (LAN, VPN or a public
/// domain), check that it answers and keep a short list of recent ones.
class ServerPage extends ConsumerStatefulWidget {
  const ServerPage({super.key});

  @override
  ConsumerState<ServerPage> createState() => _ServerPageState();
}

class _ServerPageState extends ConsumerState<ServerPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _urlController;

  bool _probing = false;
  bool _saving = false;
  ServerProbeResult? _result;
  ServerProbeException? _failure;
  bool _diagnosing = false;
  List<DiagnosticResult>? _diagnostics;
  NetworkReport? _network;

  @override
  void initState() {
    super.initState();
    _urlController = TextEditingController(text: ref.read(serverConfigProvider).baseUrl);
  }

  @override
  void dispose() {
    _urlController.dispose();
    super.dispose();
  }

  void _clearResult() {
    _result = null;
    _failure = null;
  }

  void _select(String url) {
    setState(() {
      _urlController.text = url;
      _clearResult();
    });
  }

  Future<void> _probe() async {
    if (!_formKey.currentState!.validate()) return;
    final url = normalizeServerUrl(_urlController.text);
    final host = serverHost(url);
    final pinned = host == null ? null : ref.read(serverConfigProvider).trustedFingerprints[host];
    setState(() {
      _probing = true;
      _clearResult();
    });
    try {
      final result = await ref.read(serverProbeProvider).check(url, pinnedFingerprint: pinned);
      if (!mounted) return;
      setState(() => _result = result);
    } on ServerProbeException catch (e) {
      if (!mounted) return;
      setState(() => _failure = e);
    } catch (_) {
      if (!mounted) return;
      setState(() => _failure = const ServerProbeException(ServerProbeFailure.unreachable));
    } finally {
      if (mounted) setState(() => _probing = false);
    }
  }

  Future<void> _diagnose() async {
    setState(() {
      _diagnosing = true;
      _diagnostics = const [];
      _network = null;
    });
    try {
      final diagnostics = ref.read(diagnosticsProvider);
      final network = diagnostics.network;
      if (network != null) {
        final report = await network();
        if (mounted) setState(() => _network = report);
      }
      await diagnostics.run(
        onProgress: (results) {
          if (mounted) setState(() => _diagnostics = results);
        },
      );
    } finally {
      if (mounted) setState(() => _diagnosing = false);
    }
  }

  Future<void> _trustAndRetry(String fingerprint) async {
    final host = serverHost(normalizeServerUrl(_urlController.text));
    if (host == null) return;
    await ref.read(serverConfigProvider.notifier).trustFingerprint(host, fingerprint);
    if (!mounted) return;
    await _probe();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await ref.read(serverConfigProvider.notifier).setBaseUrl(_urlController.text);
      if (!mounted) return;
      final auth = ref.read(authControllerProvider);
      context.go(auth is AuthSignedIn ? AppRoutes.home : AppRoutes.login);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final config = ref.watch(serverConfigProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Servidor')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Form(
                  key: _formKey,
                  child: TextFormField(
                    key: const Key('server-url'),
                    controller: _urlController,
                    keyboardType: TextInputType.url,
                    textInputAction: TextInputAction.done,
                    autocorrect: false,
                    onChanged: (_) => setState(_clearResult),
                    onFieldSubmitted: (_) => _probing ? null : _probe(),
                    decoration: const InputDecoration(
                      labelText: 'Dirección del servidor',
                      hintText: 'http://192.168.1.50:8080',
                      helperText:
                          'IP o dominio, con puerto si hace falta. Sin esquema se usa http://.',
                      border: OutlineInputBorder(),
                    ),
                    validator: validateServerUrl,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Certificados de usuario reconocidos: ${ref.watch(trustedUserCertificateCountProvider)}',
                  key: const Key('server-user-certs'),
                  style: theme.textTheme.bodySmall,
                ),
                const SizedBox(height: 16),
                OutlinedButton.icon(
                  key: const Key('server-test'),
                  onPressed: _probing || _saving ? null : _probe,
                  icon: _probing
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.wifi_tethering),
                  label: const Text('Probar conexión'),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  key: const Key('server-diagnose'),
                  onPressed: _diagnosing || _probing || _saving || config.baseUrl.isEmpty
                      ? null
                      : _diagnose,
                  icon: _diagnosing
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.network_check),
                  label: const Text('Diagnosticar conexión'),
                ),
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    'Comprueba el servidor guardado, tu sesión y la conexión en vivo.',
                    style: theme.textTheme.bodySmall,
                  ),
                ),
                if (_network != null) ...[
                  const SizedBox(height: 12),
                  _NetworkLines(report: _network!),
                ],
                if (_diagnostics != null && _diagnostics!.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  for (final result in _diagnostics!) _DiagnosticLine(result: result),
                ],
                if (_result != null) ...[
                  const SizedBox(height: 12),
                  Text(
                    'Conectado a ${_result!.name} v${_result!.version}',
                    key: const Key('server-result'),
                    style: theme.textTheme.bodyMedium?.copyWith(color: Colors.green.shade700),
                  ),
                ],
                if (_failure != null) ..._failureWidgets(theme, _failure!),
                const SizedBox(height: 16),
                FilledButton(
                  key: const Key('server-save'),
                  onPressed: _probing || _saving ? null : _save,
                  child: const Text('Guardar y continuar'),
                ),
                if (config.recentUrls.isNotEmpty) ...[
                  const SizedBox(height: 32),
                  Text('Servidores recientes', style: theme.textTheme.titleSmall),
                  const SizedBox(height: 4),
                  Text(
                    'Toca para seleccionar; desliza para quitar.',
                    style: theme.textTheme.bodySmall,
                  ),
                  const SizedBox(height: 8),
                  for (final url in config.recentUrls)
                    Dismissible(
                      key: ValueKey('recent-$url'),
                      direction: DismissDirection.horizontal,
                      background: Container(
                        color: theme.colorScheme.errorContainer,
                        alignment: Alignment.centerLeft,
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        child: const Icon(Icons.delete_outline),
                      ),
                      secondaryBackground: Container(
                        color: theme.colorScheme.errorContainer,
                        alignment: Alignment.centerRight,
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        child: const Icon(Icons.delete_outline),
                      ),
                      onDismissed: (_) => ref.read(serverConfigProvider.notifier).removeRecent(url),
                      child: ListTile(
                        key: Key('server-recent-$url'),
                        leading: Icon(url == config.baseUrl ? Icons.check_circle : Icons.history),
                        title: Text(url),
                        onTap: () => _select(url),
                      ),
                    ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  List<Widget> _failureWidgets(ThemeData theme, ServerProbeException failure) {
    final fingerprint = failure.fingerprint;
    return [
      const SizedBox(height: 12),
      Text(
        failure.message,
        key: const Key('server-error'),
        style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
      ),
      if (failure.failure == ServerProbeFailure.certificate) ...[
        const SizedBox(height: 8),
        Text(
          'Si tu servidor usa una CA propia, instálala en Ajustes → Seguridad → '
          'Credenciales de usuario y vuelve a abrir la app, o confía en este certificado.',
          key: const Key('server-ca-hint'),
          style: theme.textTheme.bodySmall,
        ),
      ],
      if (failure.failure == ServerProbeFailure.certificate && fingerprint != null) ...[
        const SizedBox(height: 12),
        Text('Huella SHA-256 del certificado:', style: theme.textTheme.bodySmall),
        const SizedBox(height: 4),
        SelectableText(
          fingerprint,
          key: const Key('server-fingerprint'),
          style: theme.textTheme.bodySmall?.copyWith(fontFamily: 'monospace'),
        ),
        const SizedBox(height: 4),
        Text(
          'Confía solo si coincide con la huella de tu servidor.',
          style: theme.textTheme.bodySmall,
        ),
        const SizedBox(height: 8),
        FilledButton.tonal(
          key: const Key('server-trust'),
          onPressed: _probing ? null : () => _trustAndRetry(fingerprint),
          child: const Text('Confiar en este certificado'),
        ),
      ],
    ];
  }
}

/// The network in use and what the server host resolves to from it.
class _NetworkLines extends StatelessWidget {
  const _NetworkLines({required this.report});

  final NetworkReport report;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final resolution = report.resolutionLine;
    final hint = report.hint;
    return Row(
      key: const Key('diagnose-network'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.router_outlined, size: 20),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(report.networkLine, style: theme.textTheme.titleSmall),
              if (resolution != null)
                Text(
                  resolution,
                  key: const Key('diagnose-resolution'),
                  style: theme.textTheme.bodySmall,
                ),
              if (hint != null)
                Text(
                  hint,
                  key: const Key('diagnose-network-hint'),
                  style: theme.textTheme.bodySmall?.copyWith(fontStyle: FontStyle.italic),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

class _DiagnosticLine extends StatelessWidget {
  const _DiagnosticLine({required this.result});

  final DiagnosticResult result;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (icon, color) = switch (result.outcome) {
      DiagnosticOutcome.ok => (Icons.check_circle, Colors.green.shade700),
      DiagnosticOutcome.failed => (Icons.cancel, theme.colorScheme.error),
      DiagnosticOutcome.skipped => (Icons.remove_circle_outline, theme.disabledColor),
    };
    final number = DiagnosticStep.values.indexOf(result.step) + 1;
    final warning = result.ok && result.cause != null;
    return Padding(
      key: Key('diagnose-step-${result.step.name}'),
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            warning ? Icons.warning_amber_rounded : icon,
            key: Key('diagnose-icon-${result.step.name}-${result.outcome.name}'),
            size: 20,
            color: warning ? Colors.amber.shade800 : color,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('$number · ${result.step.title}', style: theme.textTheme.titleSmall),
                Text(result.detail, style: theme.textTheme.bodySmall),
                if (result.cause != null)
                  Text(
                    result.cause!,
                    key: Key('diagnose-cause-${result.step.name}'),
                    style: theme.textTheme.bodySmall?.copyWith(fontStyle: FontStyle.italic),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
