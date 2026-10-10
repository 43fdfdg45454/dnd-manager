import 'package:flutter/material.dart';

import '../../../core/theme/app_icon.dart';
import '../../../core/theme/contrast.dart';
import '../../../core/theme/icons.dart';
import '../../../core/theme/tokens.dart';

/// What a spell or a feature costs on the character's turn (SRD casting time
/// or the feature's own text): an action, a bonus action, a reaction or
/// something else (minutes, hours...).
enum ActionKind {
  action,
  bonusAction,
  reaction,
  other;

  /// Spanish label; the time of [other] is in [ActionTiming.label].
  String get label => switch (this) {
    action => 'Acción',
    bonusAction => 'Acción adicional',
    reaction => 'Reacción',
    other => 'Otro',
  };

  /// Icon of the chip.
  AppIcons get icon => switch (this) {
    action => AppIcons.sword,
    bonusAction => AppIcons.bolt,
    reaction => AppIcons.shield,
    other => AppIcons.clock,
  };

  /// Accent of the kind, from the palette tokens.
  Color color(AppTokens t) => switch (this) {
    action => t.ember,
    bonusAction => t.oldGold,
    reaction => t.arcane,
    other => t.boneMuted,
  };

  /// Text colour of the chip: the readable variant of [color], kept readable
  /// on the chip's tinted background over every surface.
  Color textColor(AppTokens t) {
    final base = switch (this) {
      action => t.emberText,
      bonusAction => t.oldGoldText,
      reaction => t.arcaneText,
      other => t.boneMuted,
    };
    final tint = color(t).withValues(alpha: ActionTypeChip.backgroundAlpha);
    return readableOn(base, [
      for (final surface in [t.obsidian, t.stone, t.stoneRaised]) Color.alphaBlend(tint, surface),
    ], toward: t.bone);
  }

  /// Kind, Spanish label and trigger of an SRD casting time: "1 action",
  /// "1 bonus action", "1 reaction, which you take when..." or a duration
  /// ("10 minutes" → "10 minutos"). Unknown texts are kept as they are.
  static ActionTiming fromCastingTime(String? castingTime) {
    final text = (castingTime ?? '').trim();
    final lower = text.toLowerCase();
    if (lower == '1 action') return const ActionTiming(ActionKind.action);
    if (lower == '1 bonus action') return const ActionTiming(ActionKind.bonusAction);
    if (lower.startsWith('1 reaction')) {
      final comma = text.indexOf(',');
      final note = comma < 0 ? '' : text.substring(comma + 1).trim();
      return ActionTiming(ActionKind.reaction, note: note.isEmpty ? null : note);
    }
    final duration = RegExp(r'^(\d+)\s+(minutes?|hours?)$').firstMatch(lower);
    if (duration != null) {
      final n = int.parse(duration.group(1)!);
      final unit = duration.group(2)!.startsWith('minute')
          ? (n == 1 ? 'minuto' : 'minutos')
          : (n == 1 ? 'hora' : 'horas');
      return ActionTiming(ActionKind.other, text: '$n $unit');
    }
    return ActionTiming(ActionKind.other, text: text.isEmpty ? null : text);
  }
}

/// An [ActionKind] with the time of an [ActionKind.other] and the trigger of a
/// reaction ([note], in the SRD's English).
@immutable
class ActionTiming {
  const ActionTiming(this.kind, {this.text, this.note});

  final ActionKind kind;

  /// Spanish time of an [ActionKind.other] ("1 minuto").
  final String? text;

  /// When a reaction is taken ("which you take when you are hit by...").
  final String? note;

  /// "Acción", "Acción adicional", "Reacción" or the time ("1 minuto").
  String get label => kind == ActionKind.other ? (text ?? kind.label) : kind.label;

  @override
  bool operator ==(Object other) =>
      other is ActionTiming && other.kind == kind && other.text == text && other.note == note;

  @override
  int get hashCode => Object.hash(kind, text, note);

  @override
  String toString() => 'ActionTiming($kind, $text, $note)';
}

/// Small chip with the icon and the label of an [ActionKind], coloured from
/// the palette tokens (the same in the whole app). Its body is keyed
/// `action-kind-<kind.name>`.
class ActionTypeChip extends StatelessWidget {
  const ActionTypeChip(this.kind, {super.key, this.label, this.compact = false});

  /// The chip of an SRD casting time ([ActionKind.fromCastingTime]).
  factory ActionTypeChip.castingTime(String? castingTime, {Key? key, bool compact = false}) {
    final timing = ActionKind.fromCastingTime(castingTime);
    return ActionTypeChip(timing.kind, key: key, label: timing.label, compact: compact);
  }

  /// Opacity of the background tint.
  static const backgroundAlpha = 0.16;

  final ActionKind kind;

  /// Replaces [ActionKind.label] (the time of an [ActionKind.other]).
  final String? label;

  /// Smaller, for list rows.
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final theme = Theme.of(context);
    final color = kind.color(tokens);
    final text = kind.textColor(tokens);
    final style = (compact ? theme.textTheme.labelSmall : theme.textTheme.labelMedium)?.copyWith(
      color: text,
    );
    final name = label ?? kind.label;
    return Semantics(
      label: 'Tipo de acción: $name',
      excludeSemantics: true,
      child: Container(
        key: Key('action-kind-${kind.name}'),
        padding: EdgeInsets.symmetric(horizontal: compact ? 6 : 8, vertical: compact ? 1 : 3),
        decoration: BoxDecoration(
          color: color.withValues(alpha: backgroundAlpha),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: color.withValues(alpha: 0.6)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            AppIcon(kind.icon, size: compact ? 12 : 14, color: text),
            SizedBox(width: compact ? 3 : 4),
            Flexible(
              child: Text(name, style: style, overflow: TextOverflow.ellipsis),
            ),
          ],
        ),
      ),
    );
  }
}

/// Subtitle of a spell row: the compact [ActionTypeChip] of [castingTime]
/// (when known) beside [text].
class CastingTimeSubtitle extends StatelessWidget {
  const CastingTimeSubtitle({super.key, required this.castingTime, required this.text});

  final String? castingTime;
  final String text;

  @override
  Widget build(BuildContext context) {
    if (castingTime == null) return Text(text);
    return Wrap(
      spacing: 6,
      runSpacing: 2,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [ActionTypeChip.castingTime(castingTime, compact: true), Text(text)],
    );
  }
}
