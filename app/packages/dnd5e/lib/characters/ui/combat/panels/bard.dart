import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../ui/action_type.dart';

import 'panel_support.dart';

/// Bardic Inspiration die by bard level (SRD): d6, d8 at 5, d10 at 10, d12 at 15.
String bardicInspirationDie(int level) => level >= 15
    ? 'd12'
    : level >= 10
    ? 'd10'
    : level >= 5
    ? 'd8'
    : 'd6';

/// Song of Rest die by bard level (from 2); null before.
String? songOfRestDie(int level) => level < 2
    ? null
    : level >= 17
    ? 'd12'
    : level >= 13
    ? 'd10'
    : level >= 9
    ? 'd8'
    : 'd6';

/// Bard: Bardic Inspiration (resource `bardic-inspiration`) on the turn and
/// the Song of Rest die to remember.
const bardPanel = ClassPanelSpec(actions: _actions, more: _more);

List<Widget> _actions(ClassPanelContext panel, WidgetRef ref) {
  // The server sends the die with the resource; older servers do not.
  final die =
      findResource(panel.character, 'bardic-inspiration')?.dice ??
      bardicInspirationDie(panel.level);
  return [
    ClassResourceActionRow(
      panel: panel,
      resourceKey: 'bardic-inspiration',
      title: 'Inspiración bárdica',
      featureIndex: 'bardic-inspiration-d6',
      // SRD: "You can inspire others ... use a bonus action on your turn".
      actionKind: ActionKind.bonusAction,
      actionKey: 'bard-inspire',
      buttonLabel: 'Inspirar (1$die)',
      icon: AppIcons.bard,
      success: 'Inspiración bárdica concedida: 1$die.',
      description:
          'Una criatura que te oiga gana un dado $die para sumarlo a una prueba, ataque o '
          'salvación en los próximos 10 minutos.',
      extra: [Text('Dado: $die', key: const Key('bard-inspiration-die'))],
    ),
  ];
}

List<Widget> _more(ClassPanelContext panel, WidgetRef ref) {
  final song = songOfRestDie(panel.level);
  return [
    if (song != null)
      FeatureReminder(
        key: const Key('bard-song-of-rest'),
        icon: AppIcons.bard,
        title: 'Canción de descanso',
        featureIndex: 'song-of-rest-d6',
        text:
            'Curación extra: 1$song. Durante un descanso corto, quienes gasten dados de golpe '
            'recuperan 1$song PG más.',
      ),
  ];
}
