import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../../core/theme/app_icon.dart';
import '../../../../../core/theme/icons.dart';

import '../../../data/characters_controller.dart';
import '../../../data/models.dart';
import '../../../domain/class_theme.dart';
import '../combat_support.dart';
import '../resources_section.dart' show canRestoreResource, resourcesOf;

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

typedef ClassPanelBuilder = Widget Function(BuildContext context, ClassPanelContext panel);

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

CharacterController panelController(WidgetRef ref, CharacterDetail character) =>
    ref.read(characterControllerProvider(character.id).notifier);

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

/// Largest resource drawn as dots; bigger ones are a bar.
const _maxPips = 20;

/// Remaining uses of [resource] (0 when missing).
int remainingOf(CharacterResource? resource) =>
    resource == null ? 0 : (resource.max - resource.used).clamp(0, resource.max);

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

// -- Widgets ------------------------------------------------------------------

/// Header (class icon, Spanish name and level) and cards of a class panel,
/// keyed `class-panel-<index>`.
class ClassPanelFrame extends StatelessWidget {
  const ClassPanelFrame({super.key, required this.panel, required this.children});

  final ClassPanelContext panel;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final classTheme = classThemeOf(panel.classIndex);
    return Column(
      key: Key('class-panel-${panel.classIndex}'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 20, bottom: 8),
          child: Row(
            children: [
              AppIcon(classTheme.icon, size: 22, color: theme.colorScheme.primary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  '${classTheme.labelEs} (nivel ${panel.level})',
                  style: theme.textTheme.titleMedium,
                ),
              ),
            ],
          ),
        ),
        ...children,
      ],
    );
  }
}

/// Uses of a class resource looked up by key: dots (tap spends one, long press
/// gives one back), a bar beyond 20, "Usos ilimitados" at the unlimited value.
/// Shows "Recurso no disponible" when the character lacks it, or the level
/// that grants it when [level] is below [minLevel].
class ClassResourceUses extends ConsumerWidget {
  const ClassResourceUses({
    super.key,
    required this.panel,
    required this.resource,
    required this.keyPrefix,
    required this.label,
    this.minLevel = 1,
    this.tapToSpend = true,
  });

  final ClassPanelContext panel;
  final CharacterResource? resource;

  /// Keys: `<prefix>-uses` (text), `<prefix>-pips`, `<prefix>-unavailable`.
  final String keyPrefix;
  final String label;
  final int minLevel;

  /// False when the panel has its own buttons to spend (the dots only show).
  final bool tapToSpend;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final r = resource;
    if (panel.level < minLevel) {
      return Text(
        '$label: se obtiene a nivel $minLevel.',
        key: Key('$keyPrefix-locked'),
        style: theme.textTheme.bodySmall,
      );
    }
    if (r == null || r.max <= 0) {
      return Row(
        key: Key('$keyPrefix-unavailable'),
        children: [
          Icon(Icons.info_outline, size: 18, color: theme.colorScheme.onSurfaceVariant),
          const SizedBox(width: 6),
          Expanded(child: Text('$label: Recurso no disponible', style: theme.textTheme.bodySmall)),
        ],
      );
    }
    final remaining = remainingOf(r);
    final canEdit = panel.canEdit;
    VoidCallback? spend = canEdit && tapToSpend
        ? () => spendClassResource(context, ref, panel.character, r)
        : null;
    VoidCallback? restore = canEdit && canRestoreResource(r, isDm: panel.isDm)
        ? () => restoreClassResource(context, ref, panel.character, r)
        : null;

    final Widget body;
    if (r.max >= unlimitedUses) {
      body = const Text('Usos ilimitados');
      spend = null;
      restore = null;
    } else if (r.max > _maxPips) {
      body = InkWell(
        key: Key('$keyPrefix-pips'),
        onTap: spend,
        onLongPress: restore,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: LinearProgressIndicator(
            value: remaining / r.max,
            minHeight: 10,
            borderRadius: BorderRadius.circular(5),
          ),
        ),
      );
    } else {
      body = PipRow(
        key: Key('$keyPrefix-pips'),
        total: r.max,
        filled: remaining,
        semanticLabel: '$label: $remaining de ${r.max}',
        onTap: spend,
        onLongPress: restore,
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(label, style: theme.textTheme.bodyLarge)),
            Text(
              r.max >= unlimitedUses ? '∞' : '$remaining / ${r.max}',
              key: Key('$keyPrefix-uses'),
              style: numericStyle(theme.textTheme.titleSmall),
            ),
          ],
        ),
        body,
        Text(
          r.recharge.label,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
      ],
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
      padding: const EdgeInsets.symmetric(vertical: 4),
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

/// Card of a class resource with a button that spends one use: the uses
/// ([ClassResourceUses], keys `<actionKey>-uses` / `-pips`) and the button
/// keyed [actionKey]. [onUse] replaces the plain spend (e.g. Second Wind also
/// heals). Below [minLevel] only says when the feature arrives.
class ClassResourceActionCard extends ConsumerWidget {
  const ClassResourceActionCard({
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
    this.trailing,
    this.extra = const [],
  });

  final ClassPanelContext panel;
  final String resourceKey;
  final String title;
  final String actionKey;
  final String buttonLabel;
  final AppIcons icon;
  final int minLevel;
  final String? success;
  final String? description;
  final Future<void> Function(BuildContext context, WidgetRef ref, CharacterResource resource)?
  onUse;
  final Widget? trailing;
  final List<Widget> extra;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final resource = findResource(panel.character, resourceKey);
    final unlocked = panel.level >= minLevel;
    final usable =
        resource != null &&
        resource.max > 0 &&
        (resource.max >= unlimitedUses || remainingOf(resource) > 0);
    return CombatCard(
      title: title,
      trailing: trailing,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClassResourceUses(
            panel: panel,
            resource: resource,
            keyPrefix: actionKey,
            label: 'Usos',
            minLevel: minLevel,
            tapToSpend: false,
          ),
          if (unlocked)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton.tonalIcon(
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
                  icon: AppIcon(icon, size: 20),
                  label: Text(buttonLabel),
                ),
              ),
            ),
          if (description != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(description!, style: theme.textTheme.bodySmall),
            ),
          ...extra,
        ],
      ),
    );
  }
}

/// A read-only reminder of a class feature, keyed for tests.
class FeatureReminder extends StatelessWidget {
  const FeatureReminder({super.key, required this.icon, required this.title, required this.text});

  final AppIcons icon;
  final String title;
  final String text;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      leading: AppIcon(icon, color: Theme.of(context).colorScheme.primary),
      title: Text(title),
      subtitle: Text(text),
    );
  }
}
