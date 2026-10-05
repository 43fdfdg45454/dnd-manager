import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../data/server_info_repository.dart';

/// Pantalla inicial de la fase 0: confirma que la app llega al servidor.
/// Se sustituirá por el login en la fase de autenticación.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final serverInfo = ref.watch(serverInfoProvider);

    return Scaffold(
      appBar: AppBar(title: const Text(AppConfig.appName)),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.casino_outlined, size: 72),
              const SizedBox(height: 16),
              Text('Servidor: ${AppConfig.apiBaseUrl}',
                  style: Theme.of(context).textTheme.bodySmall, textAlign: TextAlign.center),
              const SizedBox(height: 24),
              serverInfo.when(
                loading: () => const CircularProgressIndicator(),
                data: (info) => Text(
                  'Conectado a ${info.name} v${info.version}',
                  key: const Key('server-status'),
                  textAlign: TextAlign.center,
                ),
                error: (error, _) => Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'No se pudo conectar con el servidor.',
                      key: const Key('server-status'),
                      style: TextStyle(color: Theme.of(context).colorScheme.error),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: () => ref.invalidate(serverInfoProvider),
                      icon: const Icon(Icons.refresh),
                      label: const Text('Reintentar'),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
