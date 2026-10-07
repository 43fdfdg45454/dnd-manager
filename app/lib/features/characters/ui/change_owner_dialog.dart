import 'package:flutter/material.dart';

import '../../campaigns/domain/campaign_models.dart';

/// Lets a DM hand a character to one of the [members] with the `Player` role
/// or turn it into an NPC. Pops with the chosen owner (`(userId: null)` is an
/// NPC), or null when cancelled. The current owner [currentOwnerUserId] is
/// marked and cannot be chosen again.
Future<({String? userId})?> pickCharacterOwner(
  BuildContext context, {
  required String characterName,
  required List<Member> members,
  required String? currentOwnerUserId,
}) => showDialog<({String? userId})>(
  context: context,
  builder: (_) => ChangeOwnerDialog(
    characterName: characterName,
    members: members,
    currentOwnerUserId: currentOwnerUserId,
  ),
);

class ChangeOwnerDialog extends StatelessWidget {
  const ChangeOwnerDialog({
    super.key,
    required this.characterName,
    required this.members,
    required this.currentOwnerUserId,
  });

  final String characterName;
  final List<Member> members;
  final String? currentOwnerUserId;

  @override
  Widget build(BuildContext context) {
    final players = [
      for (final m in members)
        if (m.role == CampaignRole.player) m,
    ];
    Widget option({
      required Key key,
      required String? userId,
      required Widget icon,
      required String label,
    }) {
      final current = userId == currentOwnerUserId;
      return ListTile(
        key: key,
        leading: icon,
        title: Text(label, overflow: TextOverflow.ellipsis),
        trailing: current ? const Icon(Icons.check) : null,
        subtitle: current ? const Text('Actual') : null,
        enabled: !current,
        onTap: () => Navigator.of(context).pop<({String? userId})>((userId: userId)),
      );
    }

    return AlertDialog(
      key: const Key('character-owner-dialog'),
      title: Text('Jugador de $characterName'),
      contentPadding: const EdgeInsets.symmetric(vertical: 8),
      content: SizedBox(
        width: double.maxFinite,
        child: ListView(
          shrinkWrap: true,
          children: [
            for (final m in players)
              option(
                key: Key('character-owner-${m.userId}'),
                userId: m.userId,
                icon: const Icon(Icons.person_outline),
                label: m.displayName,
              ),
            if (players.isEmpty) const ListTile(title: Text('No hay jugadores en la campaña.')),
            const Divider(),
            option(
              key: const Key('character-owner-npc'),
              userId: null,
              icon: const Icon(Icons.theater_comedy_outlined),
              label: 'Convertir en PNJ',
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
      ],
    );
  }
}
