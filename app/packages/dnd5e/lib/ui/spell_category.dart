import 'package:flutter/material.dart';

import 'package:opentrpg_core/core/theme/app_icon.dart';
import 'package:opentrpg_core/core/theme/icons.dart';
import 'package:opentrpg_core/core/theme/tokens.dart';

/// What a spell is mainly for (`SpellCategory` of the server).
enum SpellCategory {
  healing('Healing', 'Curación', AppIcons.drop),
  damage('Damage', 'Daño', AppIcons.flame),
  control('Control', 'Control', AppIcons.chains),
  buff('Buff', 'Apoyo', AppIcons.sparkles),
  defense('Defense', 'Defensa', AppIcons.shield),
  utility('Utility', 'Utilidad', AppIcons.compass),
  summoning('Summoning', 'Invocación', AppIcons.rune);

  const SpellCategory(this.apiValue, this.label, this.icon);

  final String apiValue;

  /// Spanish name, used as tooltip and filter label.
  final String label;
  final AppIcons icon;

  /// Colour of the icon.
  Color color(AppTokens tokens) => switch (this) {
    healing => tokens.moss,
    damage => tokens.blood,
    control => tokens.arcane,
    buff => tokens.oldGold,
    defense => tokens.bone,
    utility => tokens.boneMuted,
    summoning => tokens.ember,
  };

  /// The category named [value] (as the API sends it), or null.
  static SpellCategory? fromApi(String? value) {
    for (final c in values) {
      if (c.apiValue == value) return c;
    }
    return null;
  }
}

/// The icon of a spell category with its colour and a Spanish tooltip. Shows
/// nothing when [category] is null or unknown.
class SpellCategoryIcon extends StatelessWidget {
  const SpellCategoryIcon(this.category, {super.key, this.size = 20});

  /// `SpellCategory` name as the API sends it.
  final String? category;
  final double size;

  @override
  Widget build(BuildContext context) {
    final value = SpellCategory.fromApi(category);
    if (value == null) return const SizedBox.shrink();
    return Tooltip(
      message: value.label,
      child: AppIcon(
        value.icon,
        key: Key('spell-category-${value.apiValue}'),
        size: size,
        color: value.color(context.tokens),
        semanticLabel: value.label,
      ),
    );
  }
}
