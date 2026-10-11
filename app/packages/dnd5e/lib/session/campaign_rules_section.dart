import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/network/api_error.dart';
import 'package:opentrpg_core/core/ui/offline_widgets.dart';

import 'campaign_rules.dart';

/// The D&D 5e rules of a campaign under its content packs (Owner/DM): the
/// "Dotes iniciales" switch, only while some enabled pack brings starter
/// feats. Nothing while loading or when the rules cannot be read.
class CampaignRulesSection extends ConsumerStatefulWidget {
  const CampaignRulesSection({super.key, required this.campaignId});

  final String campaignId;

  @override
  ConsumerState<CampaignRulesSection> createState() => _CampaignRulesSectionState();
}

class _CampaignRulesSectionState extends ConsumerState<CampaignRulesSection> {
  bool _saving = false;

  Future<void> _set(bool enabled) async {
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _saving = true);
    try {
      await ref
          .read(campaignRulesControllerProvider(widget.campaignId).notifier)
          .setStarterFeats(enabled);
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(enabled ? 'Dotes iniciales activadas.' : 'Dotes iniciales desactivadas.'),
          ),
        );
    } catch (error) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              describeApiError(
                error,
                byStatus: const {
                  403: 'Solo el dueño o un DM pueden cambiar las reglas de la campaña.',
                },
              ),
            ),
          ),
        );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final rules = ref.watch(campaignRulesControllerProvider(widget.campaignId)).value;
    if (rules == null || !rules.starterFeatsAvailable) return const SizedBox.shrink();
    return OfflineAware(
      builder: (context, canWrite) => SwitchListTile(
        key: const Key('campaign-rules-starter-feats'),
        value: rules.starterFeatsEnabled,
        onChanged: canWrite && !_saving ? _set : null,
        title: const Text('Dotes iniciales'),
        subtitle: const Text(
          'Al crear un personaje, ofrece elegir una dote inicial de los paquetes activos '
          '(opcional, solo a nivel 1).',
        ),
      ),
    );
  }
}
