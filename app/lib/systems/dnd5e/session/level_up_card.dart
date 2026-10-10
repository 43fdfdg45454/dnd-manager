import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/motion/pulse.dart';
import '../../../core/theme/app_icon.dart';
import '../../../core/theme/components.dart';
import '../../../core/theme/icons.dart';
import '../../../core/theme/tokens.dart';
import '../characters/models.dart';
import '../dnd5e_routes.dart';

/// "¡Puedes subir a nivel N!": a DM granted the next level, with the level-up
/// glyph beating softly ([PulseSeal]). Its button opens the level-up wizard.
class LevelUpCard extends StatelessWidget {
  const LevelUpCard({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final level = character.pendingLevelUpTo;
    return ParchmentCard(
      key: const Key('level-up-card'),
      margin: const EdgeInsets.symmetric(vertical: 6),
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          PulseSeal(
            key: const Key('level-up-seal'),
            child: AppIcon(AppIcons.levelUp, size: 32, color: context.tokens.gold),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('¡Puedes subir a nivel $level!', style: theme.textTheme.titleMedium),
                Text('El DM te ha concedido un nivel.', style: theme.textTheme.bodySmall),
              ],
            ),
          ),
          const SizedBox(width: 8),
          FilledButton(
            key: const Key('level-up-open'),
            onPressed: () => context.push(Dnd5eRoutes.levelUp(character.id)),
            child: const Text('Subir de nivel'),
          ),
        ],
      ),
    );
  }
}
