import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';

import '../../../../ui/action_type.dart';
import '../../../dnd5e_characters_controller.dart';
import '../../../models.dart';
import '../combat_rows.dart';
import '../combat_support.dart';
import '../resources_section.dart' show resourcesOf;

/// What a class panel needs to render.
class ClassPanelContext {
  const ClassPanelContext({
    required this.character,
    required this.panel,
    required this.canEdit,
    this.isDm = false,
  });

  final CharacterDetail character;

  /// The server's panel data, or an empty one built from the class level.
  final ClassPanel panel;
  final bool canEdit;

  /// A DM or the Owner: may also restore automatic class resources.
  final bool isDm;

  String get classIndex => panel.classIndex;
  int get level => panel.level;
}

/// Rows of a class for one tab of the combat view, built inside a widget's
/// build (so [WidgetRef.watch] works); an empty list hides the class there.
typedef ClassRowsBuilder = List<Widget> Function(ClassPanelContext panel, WidgetRef ref);

/// The rows a class adds to the combat view: what it does on its turn
/// ("Acciones") and what it only needs to remember ("Más"). Its limited uses
/// are listed with the other resources ("Recursos").
class ClassPanelSpec {
  const ClassPanelSpec({required this.actions, this.more, this.spells});

  final ClassRowsBuilder actions;
  final ClassRowsBuilder? more;

  /// Rows of the class in "Conjuros" (the wizard's spellbook).
  final ClassRowsBuilder? spells;
}

// -- Data helpers -------------------------------------------------------------

int? panelInt(Object? value) =>
    value is num ? value.toInt() : (value is String ? int.tryParse(value) : null);

Map<String, dynamic> panelMap(Object? value) =>
    value is Map ? Map<String, dynamic>.from(value) : const {};

({int max, int used}) panelUses(Object? value) {
  final map = panelMap(value);
  return (max: panelInt(map['max']) ?? 0, used: panelInt(map['used']) ?? 0);
}

List<String> panelStrings(Object? value) =>
    value is List ? [for (final e in value) '$e'] : const [];

Dnd5eCharacterController panelController(WidgetRef ref, CharacterDetail character) =>
    ref.read(dnd5eCharacterControllerProvider(character.id).notifier);

/// Finds a resource by [key] or, when given, by a name fragment
/// (case-insensitive). Null when the character does not have it.
CharacterResource? findResource(CharacterDetail character, String key, [String? nameFragment]) {
  final resources = resourcesOf(character);
  for (final r in resources) {
    if (r.key == key) return r;
  }
  if (nameFragment == null) return null;
  final fragment = nameFragment.toLowerCase();
  for (final r in resources) {
    if (r.name.toLowerCase().contains(fragment)) return r;
  }
  return null;
}

/// Maximum the server uses for "unlimited" uses (rage at barbarian level 20).
const unlimitedUses = 999;

/// Remaining uses of [resource] (0 when missing).
int remainingOf(CharacterResource? resource) =>
    resource == null ? 0 : (resource.max - resource.used).clamp(0, resource.max);

/// "2 / 3", or "∞" for unlimited uses.
String usesText(CharacterResource resource) =>
    resource.max >= unlimitedUses ? '∞' : '${remainingOf(resource)} / ${resource.max}';

/// Spends [amount] of [resource] through the server; warns instead when there
/// are not enough uses left. Returns true when the server accepted it.
Future<bool> spendClassResource(
  BuildContext context,
  WidgetRef ref,
  CharacterDetail character,
  CharacterResource resource, {
  int amount = 1,
  String? success,
}) async {
  if (resource.max < unlimitedUses && remainingOf(resource) < amount) {
    showCombatMessage(context, 'No quedan usos suficientes de ${resource.name}.');
    return false;
  }
  return runCombat(
    context,
    () => panelController(ref, character).spendResource(resource.id, amount: amount),
    success: success,
  );
}

/// Gives back [amount] uses of [resource] (never above its maximum).
Future<bool> restoreClassResource(
  BuildContext context,
  WidgetRef ref,
  CharacterDetail character,
  CharacterResource resource, {
  int amount = 1,
  String? success,
}) async {
  final n = amount > resource.used ? resource.used : amount;
  if (n <= 0) return false;
  return runCombat(
    context,
    () => panelController(ref, character).restoreResource(resource.id, amount: n),
    success: success,
  );
}

// -- Rows ---------------------------------------------------------------------

/// "Se obtiene a nivel N." or "Usos: Recurso no disponible" for a class
/// resource the character cannot use yet; null when it can.
Widget? _resourceProblem(
  BuildContext context,
  ClassPanelContext panel,
  CharacterResource? resource,
  String keyPrefix,
  String label,
  int minLevel,
) {
  final theme = Theme.of(context);
  final style = theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant);
  if (panel.level < minLevel) {
    return Text(
      '$label: se obtiene a nivel $minLevel.',
      key: Key('$keyPrefix-locked'),
      style: style,
    );
  }
  if (resource == null || resource.max <= 0) {
    return Row(
      key: Key('$keyPrefix-unavailable'),
      children: [
        Icon(Icons.info_outline, size: 16, color: theme.colorScheme.onSurfaceVariant),
        const SizedBox(width: 6),
        Expanded(child: Text('$label: Recurso no disponible', style: style)),
      ],
    );
  }
  return null;
}

/// A class resource as a count to keep an eye on ("Puntos de ki 4 / 5",
/// `<keyPrefix>-uses`), with the level that grants it below [minLevel] or
/// "Recurso no disponible". Its use pips are in "Recursos".
class ClassResourceCountRow extends StatelessWidget {
  const ClassResourceCountRow({
    super.key,
    required this.panel,
    required this.resource,
    required this.keyPrefix,
    required this.title,
    required this.icon,
    this.label = 'Usos',
    this.minLevel = 1,
    this.subtitle,
    this.info,
    this.trailing,
  });

  final ClassPanelContext panel;
  final CharacterResource? resource;
  final String keyPrefix;
  final String title;
  final AppIcons icon;
  final String label;
  final int minLevel;
  final String? subtitle;
  final Widget? info;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final r = resource;
    final problem = _resourceProblem(context, panel, r, keyPrefix, label, minLevel);
    return CombatActionRow(
      title: title,
      icon: icon,
      cost: problem == null ? usesText(r!) : null,
      costKey: Key('$keyPrefix-uses'),
      subtitle: problem == null ? subtitle : null,
      info: info,
      action: trailing,
      details: [?problem],
    );
  }
}

/// A class feature that spends one use of a resource found by [resourceKey]:
/// what is left (`<actionKey>-uses`) next to the name and the button keyed
/// [actionKey] on the right. [onUse] replaces the plain spend (e.g. Second
/// Wind also heals). Below [minLevel] it only says when the feature arrives
/// (`<actionKey>-locked`); without the resource the button stays disabled
/// (`<actionKey>-unavailable`).
class ClassResourceActionRow extends ConsumerWidget {
  const ClassResourceActionRow({
    super.key,
    required this.panel,
    required this.resourceKey,
    required this.title,
    required this.actionKey,
    required this.buttonLabel,
    required this.icon,
    this.minLevel = 1,
    this.success,
    this.description,
    this.onUse,
    this.extra = const [],
    this.actionKind,
    this.featureIndex,
  });

  final ClassPanelContext panel;
  final String resourceKey;
  final String title;
  final String actionKey;
  final String buttonLabel;
  final AppIcons icon;
  final int minLevel;
  final String? success;

  /// One line saying what it does.
  final String? description;
  final Future<void> Function(BuildContext context, WidgetRef ref, CharacterResource resource)?
  onUse;

  /// Facts and controls below the row.
  final List<Widget> extra;

  /// What using the feature costs on the turn, as the SRD says; null when the
  /// feature is not an action of its own.
  final ActionKind? actionKind;

  /// SRD index of the feature for its [FeatureInfoButton], or null.
  final String? featureIndex;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resource = findResource(panel.character, resourceKey);
    final unlocked = panel.level >= minLevel;
    final problem = _resourceProblem(context, panel, resource, actionKey, 'Usos', minLevel);
    final usable =
        resource != null &&
        resource.max > 0 &&
        (resource.max >= unlimitedUses || remainingOf(resource) > 0);
    return CombatActionRow(
      title: title,
      icon: icon,
      actionKind: unlocked ? actionKind : null,
      cost: problem == null ? usesText(resource!) : null,
      costKey: Key('$actionKey-uses'),
      subtitle: description,
      info: featureIndex == null ? null : FeatureInfoButton(index: featureIndex!),
      action: unlocked
          ? FilledButton.tonalIcon(
              key: Key(actionKey),
              onPressed: panel.canEdit && usable
                  ? () => onUse != null
                        ? onUse!(context, ref, resource)
                        : spendClassResource(
                            context,
                            ref,
                            panel.character,
                            resource,
                            success: success,
                          )
                  : null,
              icon: AppIcon(icon, size: 18),
              label: Text(buttonLabel, maxLines: 1, overflow: TextOverflow.ellipsis),
            )
          : null,
      details: [?problem, ...extra],
    );
  }
}

/// A short fact of a class feature ("Ataque furtivo: 3d6").
class PanelFact extends StatelessWidget {
  const PanelFact({super.key, required this.label, required this.value, this.detail});

  final String label;
  final String value;
  final String? detail;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text.rich(
            TextSpan(
              children: [
                TextSpan(
                  text: '$label: ',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                TextSpan(text: value),
              ],
            ),
          ),
          if (detail != null)
            Text(
              detail!,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
        ],
      ),
    );
  }
}

/// A class feature to remember, without a button: [actionKind] adds its chip
/// next to the title and [featureIndex] a [FeatureInfoButton].
class FeatureReminder extends StatelessWidget {
  const FeatureReminder({
    super.key,
    required this.icon,
    required this.title,
    required this.text,
    this.actionKind,
    this.featureIndex,
    this.children = const [],
  });

  final AppIcons icon;
  final String title;
  final String text;

  /// SRD index of the feature, or null when the SRD has no entry for it.
  final String? featureIndex;

  /// What using the feature costs on the turn, as the SRD says.
  final ActionKind? actionKind;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => CombatActionRow(
    icon: icon,
    title: title,
    actionKind: actionKind,
    subtitle: text,
    info: featureIndex == null ? null : FeatureInfoButton(index: featureIndex!),
    details: children,
  );
}
