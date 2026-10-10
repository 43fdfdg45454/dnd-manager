import 'package:flutter/material.dart';
import 'package:opentrpg_core/core/theme/app_icon.dart';

import '../catalog/data/models.dart' show titleFromIndex;
import '../catalog/ui/detail_widgets.dart' show FactRow;
import 'domain/character_format.dart';
import 'domain/class_theme.dart';
import 'models.dart';
import 'ui/character_tabs.dart' show titleFromSpellIndex;

/// "Raza (subraza) · Clases · Nivel N" under the name of a character, with the
/// icon of its main class. Key: `character-subtitle` (and
/// `character-class-icon`).
class Dnd5eCharacterHeadline extends StatelessWidget {
  const Dnd5eCharacterHeadline({super.key, required this.character});

  final CharacterDetail character;

  String get _race {
    final race =
        character.raceName ??
        (character.raceIndex == null ? null : titleFromIndex(character.raceIndex!));
    if (race == null) return 'Sin raza';
    final subrace =
        character.subraceName ??
        (character.subraceIndex == null ? null : titleFromIndex(character.subraceIndex!));
    return subrace == null ? race : '$race ($subrace)';
  }

  @override
  Widget build(BuildContext context) {
    final c = character;
    final classes = c.classes.isEmpty ? 'Sin clase' : classesLabel(c.classes);
    final text = Text(
      '$_race · $classes${c.classes.isEmpty ? '' : ' · Nivel ${c.totalLevel}'}',
      key: const Key('character-subtitle'),
      style: Theme.of(context).textTheme.bodyMedium,
    );
    final main = mainClassIndex(c);
    if (main == null) return text;
    return Row(
      children: [
        AppIcon(
          classThemeOf(main).icon,
          key: const Key('character-class-icon'),
          size: 18,
          color: Theme.of(context).colorScheme.primary,
          semanticLabel: classThemeOf(main).labelEs,
        ),
        const SizedBox(width: 6),
        Flexible(child: text),
      ],
    );
  }
}

/// "Clases · Nivel N" with the icon of the main class: the short line of the
/// header of "Mi sesión".
class Dnd5eCompactHeadline extends StatelessWidget {
  const Dnd5eCompactHeadline({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = character;
    return Row(
      children: [
        AppIcon(classThemeOf(mainClassIndex(c)).icon, size: 18, color: theme.colorScheme.primary),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            '${classesLabel(c.classes)} · Nivel ${c.totalLevel}',
            style: theme.textTheme.bodyMedium,
          ),
        ),
      ],
    );
  }
}

/// "Contenido no disponible": part of the character comes from a content pack
/// that was removed. The tooltip names what is missing.
class CatalogMissingBadge extends StatelessWidget {
  const CatalogMissingBadge({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final missing = character.missingContent.join(', ');
    return Tooltip(
      message: 'Falta en el catálogo: $missing. La ficha conserva los datos.',
      triggerMode: TooltipTriggerMode.tap,
      child: Chip(
        key: const Key('catalog-missing'),
        avatar: Icon(Icons.warning_amber_rounded, size: 16, color: scheme.onErrorContainer),
        label: Text('Contenido no disponible', style: TextStyle(color: scheme.onErrorContainer)),
        backgroundColor: scheme.errorContainer,
        side: BorderSide.none,
        visualDensity: VisualDensity.compact,
      ),
    );
  }
}

/// Background and alignment, on top of "Notas".
class Dnd5eNotesFacts extends StatelessWidget {
  const Dnd5eNotesFacts({super.key, required this.character});

  final CharacterDetail character;

  @override
  Widget build(BuildContext context) {
    final c = character;
    final background =
        c.backgroundName ??
        (c.backgroundIndex == null ? null : titleFromSpellIndex(c.backgroundIndex!));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        FactRow('Trasfondo', background),
        FactRow('Alineamiento', c.alignment == null ? null : alignmentLabel(c.alignment!)),
      ],
    );
  }
}
