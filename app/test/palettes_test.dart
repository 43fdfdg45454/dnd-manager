import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/theme/app_theme.dart';
import 'package:opentrpg_dnd5e/ui/action_type.dart';

const _variants = [Brightness.dark, Brightness.light];

String _name(AppPalette palette, Brightness brightness) =>
    '${palette.name} ${brightness == Brightness.dark ? 'oscura' : 'clara'}';

Map<String, Color> _roles(AppTokens t) => {
  'obsidian': t.obsidian,
  'stone': t.stone,
  'stoneRaised': t.stoneRaised,
  'bone': t.bone,
  'boneMuted': t.boneMuted,
  'ember': t.ember,
  'arcane': t.arcane,
  'moss': t.moss,
  'blood': t.blood,
  'oldGold': t.oldGold,
  'rune': t.rune,
};

/// Smallest angle between two hues, in degrees.
double _hueDistance(double a, double b) {
  final d = (a - b).abs() % 360;
  return d > 180 ? 360 - d : d;
}

void main() {
  test('hay seis paletas con las claves del contrato y la actual por defecto', () {
    expect(AppPalette.values.map((p) => p.name), [
      'ember',
      'graphite',
      'gold',
      'forest',
      'arcane',
      'blood',
    ]);
    expect(AppPalette.ember.dark, same(AppTokens.dark));
    expect(AppPalette.ember.light, same(AppTokens.light));
    expect(AppPalette.graphite.label, 'Grafito');
    for (final palette in AppPalette.values) {
      expect(palette.label, isNotEmpty);
      expect(palette.tokens(Brightness.dark), same(palette.dark));
      expect(palette.tokens(Brightness.light), same(palette.light));
    }
  });

  group('contraste WCAG AA', () {
    for (final palette in AppPalette.values) {
      for (final brightness in _variants) {
        test(_name(palette, brightness), () {
          final t = palette.tokens(brightness);
          final surfaces = {'obsidian': t.obsidian, 'stone': t.stone, 'stoneRaised': t.stoneRaised};
          for (final surface in surfaces.entries) {
            expect(
              contrastRatio(t.bone, surface.value),
              greaterThanOrEqualTo(4.5),
              reason: 'bone sobre ${surface.key}',
            );
            expect(
              contrastRatio(t.boneMuted, surface.value),
              greaterThanOrEqualTo(4.5),
              reason: 'boneMuted sobre ${surface.key}',
            );
            // Semantic text variants (healing, damage, magic, highlight).
            for (final (role, color) in [
              ('mossText', t.mossText),
              ('bloodText', t.bloodText),
              ('arcaneText', t.arcaneText),
              ('oldGoldText', t.oldGoldText),
            ]) {
              expect(
                contrastRatio(color, surface.value),
                greaterThanOrEqualTo(4.5),
                reason: '$role sobre ${surface.key}',
              );
            }
          }

          final theme = AppTheme.build(brightness, palette: palette);
          final scheme = theme.colorScheme;
          expect(
            contrastRatio(scheme.onPrimary, t.ember),
            greaterThanOrEqualTo(4.5),
            reason: 'texto de botón sobre ember',
          );
          expect(contrastRatio(scheme.error, scheme.surface), greaterThanOrEqualTo(4.5));
          expect(contrastRatio(scheme.onError, scheme.error), greaterThanOrEqualTo(4.5));
          // Text buttons on the page and the snack bar action on its inverted
          // background.
          final textButton = theme.textButtonTheme.style!.foregroundColor!.resolve({})!;
          expect(contrastRatio(textButton, t.obsidian), greaterThanOrEqualTo(4.5));
          expect(
            contrastRatio(theme.snackBarTheme.actionTextColor!, t.bone),
            greaterThanOrEqualTo(4.5),
          );
          if (palette != AppPalette.ember) {
            // New palettes: accents are usable as icons (3:1) on the page.
            for (final (role, color) in [
              ('ember', t.ember),
              ('moss', t.moss),
              ('blood', t.blood),
              ('arcane', t.arcane),
              ('oldGold', t.oldGold),
            ]) {
              expect(
                contrastRatio(color, t.obsidian),
                greaterThanOrEqualTo(3),
                reason: '$role sobre obsidian',
              );
            }
          }
        });
      }
    }
  });

  group('tipos de acción legibles sobre stone (fase 27)', () {
    for (final palette in AppPalette.values) {
      for (final brightness in _variants) {
        test(_name(palette, brightness), () {
          final t = palette.tokens(brightness);
          for (final kind in ActionKind.values) {
            final text = kind.textColor(t);
            expect(
              contrastRatio(text, t.stone),
              greaterThanOrEqualTo(4.5),
              reason: '${kind.name} sobre stone',
            );
            // On the chip's own tint over the card.
            final chip = Color.alphaBlend(
              kind.color(t).withValues(alpha: ActionTypeChip.backgroundAlpha),
              t.stone,
            );
            expect(
              contrastRatio(text, chip),
              greaterThanOrEqualTo(4.5),
              reason: '${kind.name} sobre su chip',
            );
          }
        });
      }
    }
  });

  group('Grafito', () {
    for (final brightness in _variants) {
      test('sin naranja ni dorado (${_name(AppPalette.graphite, brightness)})', () {
        for (final role in _roles(AppPalette.graphite.tokens(brightness)).entries) {
          final hsv = HSVColor.fromColor(role.value);
          final hsl = HSLColor.fromColor(role.value);
          final orange = hsv.hue >= 15 && hsv.hue <= 45;
          expect(
            orange && (hsv.saturation > 0.35 || hsl.saturation > 0.35),
            isFalse,
            reason: role.key,
          );
        }
      });

      test(
        'semánticos desaturados pero distinguibles (${_name(AppPalette.graphite, brightness)})',
        () {
          final t = AppPalette.graphite.tokens(brightness);
          final hues = {
            'moss': HSVColor.fromColor(t.moss),
            'blood': HSVColor.fromColor(t.blood),
            'arcane': HSVColor.fromColor(t.arcane),
          };
          for (final entry in hues.entries) {
            expect(entry.value.saturation, inInclusiveRange(0.1, 0.6), reason: entry.key);
          }
          final list = hues.values.toList();
          for (var i = 0; i < list.length; i++) {
            for (var j = i + 1; j < list.length; j++) {
              expect(_hueDistance(list[i].hue, list[j].hue), greaterThan(60));
            }
          }
          // Healing still reads green, damage red and magic blue-violet.
          expect(_hueDistance(hues['moss']!.hue, 120), lessThan(30));
          expect(_hueDistance(hues['blood']!.hue, 0), lessThan(20));
          expect(_hueDistance(hues['arcane']!.hue, 245), lessThan(30));
        },
      );
    }

    test('oscura: negro con grises; clara: gris con negro', () {
      final dark = AppPalette.graphite.dark;
      final light = AppPalette.graphite.light;
      // Neutral greys: the channels differ by a few units at most (a hint of
      // cool), never towards a hue.
      double chroma(Color c) => [c.r, c.g, c.b].reduce(math.max) - [c.r, c.g, c.b].reduce(math.min);
      for (final t in [dark, light]) {
        for (final c in [t.obsidian, t.stone, t.stoneRaised, t.bone, t.boneMuted, t.ember]) {
          expect(chroma(c), lessThan(0.03));
        }
        expect(chroma(t.oldGold), lessThan(0.04));
        expect(chroma(t.rune), lessThan(0.04));
      }
      expect(relativeLuminance(dark.obsidian), lessThan(0.01));
      expect(relativeLuminance(dark.ember), greaterThan(0.5));
      expect(relativeLuminance(light.obsidian), inInclusiveRange(0.5, 0.8));
      expect(relativeLuminance(light.bone), lessThan(0.01));
      expect(relativeLuminance(light.ember), lessThan(0.02));
      expect(AppPalette.graphite.recommendsClassColors, isFalse);
    });
  });

  group('esquema derivado', () {
    for (final palette in AppPalette.values) {
      for (final brightness in _variants) {
        test(_name(palette, brightness), () {
          final t = palette.tokens(brightness);
          final theme = AppTheme.build(brightness, palette: palette);
          expect(theme.brightness, brightness);
          expect(theme.extension<AppTokens>(), same(t));
          expect(theme.colorScheme.brightness, brightness);
          expect(theme.colorScheme.primary, t.ember);
          expect(theme.colorScheme.secondary, t.oldGold);
          expect(theme.colorScheme.tertiary, t.arcane);
          expect(theme.colorScheme.surface, t.obsidian);
          expect(theme.colorScheme.onSurface, t.bone);
          expect(theme.colorScheme.onSurfaceVariant, t.boneMuted);
          expect(theme.colorScheme.surfaceContainerLow, t.stone);
          expect(theme.colorScheme.surfaceContainerHigh, t.stoneRaised);
          expect(theme.colorScheme.outline, t.rune);
          expect(theme.scaffoldBackgroundColor, t.obsidian);
          expect(theme.cardTheme.color, t.stone);
          // Dark variants are dark, light ones light.
          final pageDarker = relativeLuminance(t.obsidian) < relativeLuminance(t.bone);
          expect(pageDarker, brightness == Brightness.dark);
          expect(
            contrastRatio(theme.colorScheme.onPrimaryContainer, theme.colorScheme.primaryContainer),
            greaterThanOrEqualTo(4.5),
          );
        });
      }
    }
  });
}
