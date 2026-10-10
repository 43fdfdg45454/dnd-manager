import 'package:flutter/material.dart' hide Page;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:opentrpg_core/core/realtime/realtime_events.dart';
import 'package:opentrpg_core/features/campaigns/domain/campaign_models.dart';
import 'package:opentrpg_core/features/content_packs/domain/campaign_content_pack.dart';
import 'package:opentrpg_dnd5e/catalog/data/beast_models.dart';
import 'package:opentrpg_dnd5e/catalog/data/models.dart';

import 'helpers/app_pump.dart';
import 'helpers/catalog_fakes.dart';
import 'helpers/character_fakes.dart';
import 'helpers/content_pack_fakes.dart';
import 'helpers/fake_realtime_hub.dart';
import 'helpers/fakes.dart';

// A fictional content pack ("Reinos de Ejemplo") with a whole class, a rule
// and a creature; it requires another fictional pack ("Bestiario de Ejemplo").

const _packId = 'reinos-ejemplo';
const _bestiaryId = 'bestiario-ejemplo';

const _pack = CampaignContentPack(id: _packId, name: 'Reinos de Ejemplo', version: '1.0.0');
const _bestiary = CampaignContentPack(
  id: _bestiaryId,
  name: 'Bestiario de Ejemplo',
  version: '0.3.0',
);
const _packNeedingBestiary = CampaignContentPack(
  id: _packId,
  name: 'Reinos de Ejemplo',
  version: '1.0.0',
  requires: [_bestiaryId],
);

const _sources = [
  CatalogSource(id: 'srd', name: 'SRD 5.1', isBase: true),
  CatalogSource(id: _packId, name: 'Reinos de Ejemplo', version: '1.0.0'),
];

const _fighter = ClassSummary(index: 'fighter', name: 'Fighter', hitDie: 10, source: 'srd');
const _alchemist = ClassSummary(
  index: 'reinos-ejemplo-alquimista',
  name: 'Alquimista de Ejemplo',
  hitDie: 8,
  isSpellcaster: true,
  spellcastingAbility: 'int',
  source: _packId,
);

ClassLevel _alchemistLevel(int level, int bombs, {List<Feature> features = const []}) => ClassLevel(
  level: level,
  profBonus: 2,
  spellSlots: [level + 1, 0, 0, 0, 0, 0, 0, 0, 0],
  classSpecific: {'bombs': bombs},
  features: features,
);

final _alchemistDetail = ClassDetail(
  index: _alchemist.index,
  name: _alchemist.name,
  hitDie: 8,
  isSpellcaster: true,
  spellcastingAbility: 'int',
  source: _packId,
  description: const ['Una erudita que mezcla reactivos en plena batalla.'],
  subclassFlavor: 'Campo de estudio',
  subclassLevel: 3,
  savingThrows: const ['con', 'int'],
  skillChoices: const SkillChoices(choose: 2, from: ['arcana', 'medicine', 'nature']),
  startingEquipmentText: 'Útiles de alquimista y una daga.',
  spellcasting: const ClassSpellcasting(
    progression: 'table',
    preparation: 'prepared',
    ritual: true,
    focus: 'alchemists-supplies',
  ),
  multiclassing: const ClassMulticlassing(
    prerequisites: {'int': 13},
    armor: ['light'],
    tools: ['alchemists-supplies'],
    skills: 1,
  ),
  resources: const [
    ClassResource(key: 'bombs', name: 'Bombas', max: 'classSpecific:bombs', recharge: 'LongRest'),
    ClassResource(key: 'tonic', name: 'Tónico', max: 'mod:int', recharge: 'ShortRest'),
  ],
  levels: [
    _alchemistLevel(
      1,
      2,
      features: const [
        Feature(
          index: 'reinos-ejemplo-bombas',
          name: 'Bombas',
          description: ['Lanzas una bomba improvisada.'],
        ),
      ],
    ),
    _alchemistLevel(2, 3),
  ],
);

const _fighterDetail = ClassDetail(
  index: 'fighter',
  name: 'Fighter',
  hitDie: 10,
  savingThrows: ['str', 'con'],
  skillChoices: SkillChoices(choose: 2, from: ['athletics', 'perception']),
  levels: [ClassLevel(level: 1)],
);

const _human = RaceSummary(
  index: 'human',
  name: 'Human',
  speed: 30,
  abilityBonuses: [AbilityBonus(ability: 'str', bonus: 1)],
);

const _firearmsRule = Rule(
  index: 'reinos-ejemplo-armas-de-fuego',
  title: 'Armas de fuego de ejemplo',
  category: 'equipment',
  tags: ['pólvora'],
  source: _packId,
  body: ['Un arma de fuego se **encasquilla** con un fallo.', 'Recargarla es una acción.'],
);

const _variantRule = Rule(
  index: 'reinos-ejemplo-niveles-auxiliares',
  title: 'Niveles auxiliares de ejemplo',
  category: 'variant',
  source: _packId,
  body: ['Texto de la variante.'],
);

const _wolf = Beast(
  index: 'wolf',
  name: 'Wolf',
  size: 'Medium',
  challengeRating: 0.25,
  challengeRatingText: '1/4',
  armorClass: 13,
  hitPoints: 11,
  speeds: {'walk': 40},
  source: 'srd',
);

const _golem = Beast(
  index: 'reinos-ejemplo-golem-de-cobre',
  name: 'Gólem de cobre',
  size: 'Large',
  type: 'construct',
  subtype: 'ejemplo',
  challengeRating: 5,
  challengeRatingText: '5',
  armorClass: 17,
  hitPoints: 93,
  speeds: {'walk': 30},
  source: _packId,
  actions: [BeastAction(name: 'Golpe', description: 'Ataque cuerpo a cuerpo.', attackBonus: 7)],
  reactions: [BeastAction(name: 'Desvío', description: 'Desvía un proyectil.')],
  legendaryActions: [BeastAction(name: 'Pisotón', description: 'Golpea el suelo.')],
);

FakeCatalogRepository _catalog({Map<String, Set<String>>? enabled}) => FakeCatalogRepository(
  classList: const [_fighter, _alchemist],
  classDetails: {'fighter': _fighterDetail, _alchemist.index: _alchemistDetail},
  raceList: const [_human],
  raceDetails: const {
    'human': RaceDetail(
      index: 'human',
      name: 'Human',
      speed: 30,
      abilityBonuses: [AbilityBonus(ability: 'str', bonus: 1)],
      languages: ['Common'],
    ),
  },
  conditionList: const [
    Condition(index: 'blinded', name: 'Blinded', source: 'srd'),
    Condition(index: 'reinos-ejemplo-oxidado', name: 'Oxidado', source: _packId),
  ],
  ruleList: const [_firearmsRule, _variantRule],
  beastList: const [_wolf, _golem],
  sourceList: _sources,
  itemDetails: const {
    'pistol': ItemDetail(
      id: 'pistol',
      name: 'Pistola de ejemplo',
      category: 'Weapon',
      source: _packId,
      properties: ['Ammunition', 'Misfire'],
      systemData: {
        'special': 'Hace mucho ruido.',
        'firearm': {'reload': 4, 'misfire': 2},
      },
    ),
  },
  referenceEntries: const [
    ReferenceEntry(
      kind: 'weaponProperties',
      index: 'reinos-ejemplo-misfire',
      name: 'Misfire',
      description: ['Con ese resultado o menos en el d20, el arma se encasquilla.'],
      source: _packId,
    ),
  ],
  enabledPacks: enabled,
);

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _tapKey(WidgetTester tester, String key) => _tap(tester, find.byKey(Key(key)));

Future<void> _openTab(WidgetTester tester, String id) => _tapKey(tester, 'tab-$id');

Future<void> _pickSource(WidgetTester tester, String id) async {
  await _tapKey(tester, 'compendium-source');
  await tester.tap(find.byKey(Key('compendium-source-$id')).last);
  await tester.pumpAndSettle();
}

Future<({FakeCampaignContentPacksRepository packs, FakeCatalogRepository catalog, GoRouter router})>
_pump(
  WidgetTester tester, {
  required String location,
  CampaignRole role = CampaignRole.owner,
  List<CampaignContentPack> packs = const [srdCampaignPack, _pack],
  Set<String> enabled = const {},
  FakeRealtimeHub? hub,
  FakeCharactersRepository? characters,
}) async {
  // Shared like the server: what the settings save is what the catalog of the
  // campaign answers.
  final enabledPacks = {
    'c1': {...enabled},
  };
  final contentPacks = FakeCampaignContentPacksRepository(packs: packs, enabled: enabledPacks);
  final catalog = _catalog(enabled: enabledPacks);
  final router = await pumpRealApp(
    tester,
    location: location,
    realtime: hub,
    fakes: AppFakes(
      campaigns: FakeCampaignsRepository(campaigns: [makeCampaign(myRole: role)]),
      catalog: catalog,
      contentPacks: contentPacks,
      characters: characters,
    ),
  );
  return (packs: contentPacks, catalog: catalog, router: router);
}

void main() {
  group('modelos', () {
    test('CampaignContentPack.fromJson y los requisitos que faltan', () {
      final packs = [
        for (final json in [
          {'id': 'srd', 'name': 'SRD 5.1', 'version': '5.1', 'isBase': true, 'enabled': false},
          {
            'id': _packId,
            'name': 'Reinos',
            'version': '1',
            'isBase': false,
            'enabled': true,
            'requires': [_bestiaryId, 'srd'],
          },
          {'id': _bestiaryId, 'name': 'Bestiario', 'version': '1', 'enabled': false},
        ])
          CampaignContentPack.fromJson(json),
      ];
      // The base pack is always enabled.
      expect(packs.first.enabled, isTrue);
      expect(packs[1].requires, [_bestiaryId, 'srd']);
      expect(missingPackRequirements(packs, {_packId}), {
        _packId: [_bestiaryId],
      });
      expect(missingPackRequirements(packs, {_packId, _bestiaryId}), isEmpty);
    });

    test('ClassDetail lee lanzamiento, multiclase, recursos y la fuente', () {
      final detail = ClassDetail.fromJson({
        'index': 'x-clase',
        'name': 'Clase X',
        'hitDie': 8,
        'source': 'x',
        'description': ['Uno.'],
        'subclassLevel': 3,
        'spellcasting': {
          'progression': 'half',
          'preparation': 'known',
          'ritual': false,
          'focus': null,
        },
        'multiclassing': {
          'prerequisites': {'dex': 13},
          'armor': ['light'],
          'weapons': <String>[],
          'tools': <String>[],
          'skills': 1,
        },
        'resources': [
          {
            'key': 'grit',
            'name': 'Grit',
            'max': 'mod:wis',
            'maxByLevel': {'1': 2, '5': 3},
            'recharge': 'ShortRest',
          },
        ],
        'levels': <Object>[],
      });
      expect(detail.source, 'x');
      expect(detail.subclassLevel, 3);
      expect(detail.spellcasting!.progression, 'half');
      expect(detail.multiclassing!.prerequisites, {'dex': 13});
      expect(detail.resources.single.maxByLevel, {1: 2, 5: 3});
      expect(ClassSummary.fromJson({'index': 'x', 'source': 'x'}).source, 'x');
    });

    test('las criaturas, condiciones y fuentes traen su paquete', () {
      final beast = Beast.fromJson({
        'index': 'g',
        'name': 'G',
        'type': 'construct',
        'subtype': 'ejemplo',
        'source': _packId,
        'reactions': [
          {'name': 'R', 'description': 'r'},
        ],
        'legendaryActions': [
          {'name': 'L', 'description': 'l'},
        ],
      });
      expect(beast.isBeast, isFalse);
      expect(beast.reactions.single.name, 'R');
      expect(beast.legendaryActions.single.name, 'L');
      expect(BeastSummary.fromJson({'index': 'w', 'name': 'W'}).isBeast, isTrue);
      expect(Condition.fromJson({'index': 'c', 'name': 'C', 'source': _packId}).source, _packId);
      final source = CatalogSource.fromJson({
        'id': _packId,
        'name': 'R',
        'isBase': false,
        'enabled': true,
      });
      expect(source.enabled, isTrue);
      expect(CatalogSource.fromJson({'id': 'srd', 'name': 'SRD'}).enabled, isNull);
      final item = ItemDetail.fromJson({
        'id': 'i',
        'name': 'I',
        'systemData': {
          'tool': true,
          'firearm': {'reload': null, 'misfire': 3},
        },
      });
      expect(item.isTool, isTrue);
      expect(item.firearmReload, isNull);
      expect(item.firearmMisfire, 3);
    });
  });

  group('ajustes de la campaña', () {
    testWidgets('el dueño activa un paquete y se guarda con PUT', (tester) async {
      final (:packs, catalog: _, router: _) = await _pump(
        tester,
        location: '/campaigns/c1/general/settings',
      );

      expect(find.byKey(const Key('campaign-packs')), findsOneWidget);
      // The base pack is fixed and marked.
      expect(find.byKey(const Key('campaign-pack-srd-on')), findsOneWidget);
      expect(find.text('Base'), findsOneWidget);
      expect(find.text('Versión 5.1 · Siempre activo'), findsOneWidget);
      final tile = find.byKey(const Key('campaign-pack-$_packId'));
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);
      expect(find.byKey(const Key('campaign-packs-save')), findsNothing);

      await _tap(tester, tile);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
      await _tapKey(tester, 'campaign-packs-save');

      expect(packs.saved.single.campaignId, 'c1');
      expect(packs.saved.single.packIds, {_packId});
      expect(find.text('Paquetes de la campaña guardados.'), findsOneWidget);
      expect(find.byKey(const Key('campaign-packs-save')), findsNothing);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
    });

    testWidgets('avisa de un paquete requerido y lo activa con un toque', (tester) async {
      final (:packs, catalog: _, router: _) = await _pump(
        tester,
        location: '/campaigns/c1/general/settings',
        packs: const [srdCampaignPack, _packNeedingBestiary, _bestiary],
      );

      expect(find.text('Versión 1.0.0 · Requiere: Bestiario de Ejemplo'), findsOneWidget);
      await _tapKey(tester, 'campaign-pack-$_packId');

      expect(find.byKey(const Key('campaign-pack-missing-$_packId')), findsOneWidget);
      expect(
        find.text('«Reinos de Ejemplo» requiere «Bestiario de Ejemplo»: actívalo también.'),
        findsOneWidget,
      );
      expect(
        tester.widget<FilledButton>(find.byKey(const Key('campaign-packs-save'))).onPressed,
        isNull,
      );

      await _tapKey(tester, 'campaign-pack-enable-required-$_packId');
      expect(find.byKey(const Key('campaign-pack-missing-$_packId')), findsNothing);
      expect(
        tester.widget<SwitchListTile>(find.byKey(const Key('campaign-pack-$_bestiaryId'))).value,
        isTrue,
      );

      await _tapKey(tester, 'campaign-packs-save');
      expect(packs.saved.single.packIds, {_packId, _bestiaryId});
    });

    testWidgets('el error del servidor se muestra en español', (tester) async {
      final (:packs, catalog: _, router: _) = await _pump(
        tester,
        location: '/campaigns/c1/general/settings',
      );
      packs.saveError = campaignPacksInvalid(
        'missing-requirement',
        "El paquete 'Reinos de Ejemplo' requiere 'Otro': actívalo también.",
      );

      await _tapKey(tester, 'campaign-pack-$_packId');
      await _tapKey(tester, 'campaign-packs-save');

      expect(
        find.text("El paquete 'Reinos de Ejemplo' requiere 'Otro': actívalo también."),
        findsOneWidget,
      );
      // The selection is kept to try again.
      expect(find.byKey(const Key('campaign-packs-save')), findsOneWidget);
    });

    testWidgets('descartar vuelve a lo guardado', (tester) async {
      await _pump(tester, location: '/campaigns/c1/general/settings', enabled: {_packId});
      final tile = find.byKey(const Key('campaign-pack-$_packId'));
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);

      await _tap(tester, tile);
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);
      await _tapKey(tester, 'campaign-packs-discard');

      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
      expect(find.byKey(const Key('campaign-packs-save')), findsNothing);
    });

    testWidgets('un jugador no ve la sección en Ajustes', (tester) async {
      await _pump(tester, location: '/campaigns/c1/general/settings', role: CampaignRole.player);

      expect(find.byKey(const Key('section-settings')), findsOneWidget);
      expect(find.byKey(const Key('campaign-packs')), findsNothing);
    });

    testWidgets('un jugador ve los paquetes en Contenido, solo lectura', (tester) async {
      await _pump(
        tester,
        location: '/campaigns/c1/general/content',
        role: CampaignRole.player,
        packs: const [srdCampaignPack, _pack, _bestiary],
        enabled: {_packId},
      );

      await _tapKey(tester, 'content-packs-expander');

      expect(find.byKey(const Key('campaign-packs')), findsOneWidget);
      expect(find.byType(SwitchListTile), findsNothing);
      expect(find.byKey(const Key('campaign-pack-$_packId-on')), findsOneWidget);
      expect(find.byKey(const Key('campaign-pack-$_bestiaryId-off')), findsOneWidget);
      expect(find.text('Versión 0.3.0 · Desactivado'), findsOneWidget);
      expect(find.byKey(const Key('campaign-packs-save')), findsNothing);
    });

    testWidgets('un DM no ve la lista en Contenido (la gestiona en Ajustes)', (tester) async {
      await _pump(tester, location: '/campaigns/c1/general/content', role: CampaignRole.dm);

      expect(find.byKey(const Key('content-packs-expander')), findsNothing);
    });

    testWidgets('campaign.updated recarga los paquetes de la campaña', (tester) async {
      final hub = FakeRealtimeHub();
      final (:packs, catalog: _, router: _) = await _pump(
        tester,
        location: '/campaigns/c1/general/content',
        role: CampaignRole.player,
        hub: hub,
      );
      await _tapKey(tester, 'content-packs-expander');
      expect(find.byKey(const Key('campaign-pack-$_packId-off')), findsOneWidget);

      // The DM enabled the pack from another device.
      packs.enabled['c1'] = {_packId};
      hub.emitJson({'type': 'campaign.updated', 'campaignId': 'c1'});
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('campaign-pack-$_packId-on')), findsOneWidget);
    });

    test('campaign.updated es un evento del núcleo', () {
      final event = CampaignEvent.fromJson({'type': 'campaign.updated', 'campaignId': 'c1'});
      expect(event, isA<CampaignUpdated>());
    });
  });

  group('compendio', () {
    testWidgets('el selector de fuente deja solo el contenido del paquete', (tester) async {
      await _pump(tester, location: '/compendium');
      await _openTab(tester, 'classes');

      expect(find.byKey(const Key('class-fighter')), findsOneWidget);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsOneWidget);
      // The pack content carries its chip; the SRD does not.
      expect(find.byKey(const Key('source-chip-$_packId')), findsOneWidget);
      expect(find.byKey(const Key('compendium-campaign-only')), findsNothing);

      await _pickSource(tester, _packId);
      expect(find.byKey(const Key('class-fighter')), findsNothing);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsOneWidget);

      await _openTab(tester, 'conditions');
      expect(find.byKey(const Key('condition-blinded')), findsNothing);
      expect(find.byKey(const Key('condition-reinos-ejemplo-oxidado')), findsOneWidget);

      await _pickSource(tester, 'srd');
      expect(find.byKey(const Key('condition-blinded')), findsOneWidget);
      expect(find.byKey(const Key('condition-reinos-ejemplo-oxidado')), findsNothing);

      await _pickSource(tester, 'all');
      expect(find.byKey(const Key('condition-blinded')), findsOneWidget);
      expect(find.byKey(const Key('condition-reinos-ejemplo-oxidado')), findsOneWidget);
    });

    testWidgets('desde la campaña solo muestra lo activo, salvo que se quite el filtro', (
      tester,
    ) async {
      final (packs: _, :catalog, :router) = await _pump(
        tester,
        location: '/campaigns/c1/general/settings',
      );
      await _tapKey(tester, 'campaign-packs-compendium');
      expect(router.state.uri.path, '/campaigns/c1/compendium');

      await _openTab(tester, 'classes');
      final chip = find.byKey(const Key('compendium-campaign-only'));
      expect(tester.widget<FilterChip>(chip).selected, isTrue);
      expect(catalog.campaignCalls, contains('c1'));
      expect(find.byKey(const Key('class-fighter')), findsOneWidget);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsNothing);

      // The disabled pack is not offered as a source either.
      await _tapKey(tester, 'compendium-source');
      expect(find.byKey(const Key('compendium-source-$_packId')), findsNothing);
      await tester.tap(find.byKey(const Key('compendium-source-all')).last);
      await tester.pumpAndSettle();

      await _tap(tester, chip);
      expect(tester.widget<FilterChip>(chip).selected, isFalse);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsOneWidget);
    });

    testWidgets('la pestaña Reglas busca y abre el texto de la regla', (tester) async {
      await _pump(tester, location: '/compendium');
      await _openTab(tester, 'rules');

      expect(find.byKey(Key('rule-${_firearmsRule.index}')), findsOneWidget);
      expect(find.byKey(Key('rule-${_variantRule.index}')), findsOneWidget);
      expect(find.text('Equipo · pólvora'), findsOneWidget);

      // Category picker.
      await _tapKey(tester, 'filter-rule-category');
      await tester.tap(find.text('Variante').last);
      await tester.pumpAndSettle();
      expect(find.byKey(Key('rule-${_firearmsRule.index}')), findsNothing);
      expect(find.byKey(Key('rule-${_variantRule.index}')), findsOneWidget);
      await _tapKey(tester, 'filter-rule-category');
      await tester.tap(find.text('Todas').last);
      await tester.pumpAndSettle();

      // Search box of the compendium (debounced).
      await tester.enterText(find.byKey(const Key('compendium-search')), 'fuego');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();
      expect(find.byKey(Key('rule-${_variantRule.index}')), findsNothing);

      await _tapKey(tester, 'rule-${_firearmsRule.index}');
      expect(find.text('Armas de fuego de ejemplo'), findsWidgets);
      expect(find.byKey(const Key('rule-body')), findsOneWidget);
      expect(find.textContaining('Recargarla es una acción.'), findsOneWidget);
      expect(find.byKey(const Key('source-chip-$_packId')), findsOneWidget);
    });

    testWidgets('sin paquetes, la pestaña Reglas lo explica', (tester) async {
      await pumpRealApp(
        tester,
        location: '/compendium',
        fakes: AppFakes(catalog: FakeCatalogRepository()),
      );
      await _openTab(tester, 'rules');

      expect(find.text('No hay reglas. Llegan con los paquetes de contenido.'), findsOneWidget);
    });

    testWidgets('el detalle de una clase nueva usa la tabla de niveles y sus recursos', (
      tester,
    ) async {
      await _pump(tester, location: '/compendium/classes/${_alchemist.index}');

      expect(find.text('Una erudita que mezcla reactivos en plena batalla.'), findsOneWidget);
      expect(find.byKey(const Key('source-chip-$_packId')), findsOneWidget);
      expect(find.textContaining('Se elige a nivel 3', findRichText: true), findsOneWidget);
      expect(find.byKey(const Key('class-level-table')), findsOneWidget);
      // The resource from the table gets a column; the formula one does not.
      expect(find.byKey(const Key('resource-1-bombs')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const Key('resource-2-bombs'))).data, '3');
      expect(find.byKey(const Key('resource-1-tonic')), findsNothing);
      expect(find.byKey(const Key('slot-2-1')), findsOneWidget);

      final spellcasting = find.byKey(const Key('class-spellcasting'));
      await tester.ensureVisible(spellcasting);
      expect(find.textContaining('Tabla propia', findRichText: true), findsOneWidget);
      expect(find.textContaining('Preparados', findRichText: true), findsOneWidget);

      final resources = find.byKey(const Key('class-resource-tonic'));
      await tester.ensureVisible(resources);
      expect(
        find.text('Máximo: Mod. de Inteligencia · Se recupera: Descanso corto'),
        findsOneWidget,
      );
      expect(
        find.text('Máximo según la tabla de niveles · Se recupera: Descanso largo'),
        findsOneWidget,
      );

      await tester.ensureVisible(find.byKey(const Key('class-multiclassing')));
      expect(find.textContaining('Inteligencia 13', findRichText: true), findsOneWidget);
      expect(find.textContaining('1 habilidad', findRichText: true), findsOneWidget);
    });

    testWidgets('las criaturas de paquetes salen en Bestias con su ficha completa', (tester) async {
      await _pump(tester, location: '/compendium');
      await _openTab(tester, 'beasts');

      expect(find.byKey(const Key('beast-wolf')), findsOneWidget);
      expect(find.byKey(Key('beast-${_golem.index}')), findsOneWidget);
      expect(find.textContaining('constructo · VD 5'), findsOneWidget);

      await _tapKey(tester, 'beast-${_golem.index}');
      expect(
        tester.widget<Text>(find.byKey(const Key('beast-type'))).data,
        'Large · constructo (ejemplo)',
      );
      expect(find.text('Reacciones'), findsOneWidget);
      expect(find.text('Acciones legendarias'), findsOneWidget);
      expect(find.byKey(const Key('beast-action-1')), findsOneWidget);
      expect(find.byKey(const Key('beast-action-2')), findsOneWidget);
      expect(find.textContaining('Contenido del SRD 5.1'), findsNothing);
    });

    testWidgets('el detalle de un arma de fuego muestra recarga, fallo y sus propiedades', (
      tester,
    ) async {
      await _pump(tester, location: '/compendium/items/pistol');

      expect(find.textContaining('4 disparos', findRichText: true), findsOneWidget);
      expect(find.textContaining('1–2 en el d20', findRichText: true), findsOneWidget);
      expect(find.textContaining('Hace mucho ruido.', findRichText: true), findsOneWidget);
      expect(find.byKey(const Key('item-property-reinos-ejemplo-misfire')), findsOneWidget);
    });
  });

  group('flujo', () {
    testWidgets('activar el paquete hace que el asistente ofrezca la clase nueva', (tester) async {
      final (packs: _, catalog: _, :router) = await _pump(
        tester,
        location: '/campaigns/c1/characters/new',
      );

      Future<void> toClassStep() async {
        await tester.enterText(find.byKey(const Key('wizard-name')), 'Merlina');
        await tester.pumpAndSettle();
        await _tapKey(tester, 'wizard-next');
        await _tapKey(tester, 'race-human');
        await _tapKey(tester, 'wizard-next');
        expect(find.byKey(const Key('step-class')), findsOneWidget);
      }

      await toClassStep();
      expect(find.byKey(const Key('class-fighter')), findsOneWidget);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsNothing);

      // The owner enables the pack in "Ajustes".
      router.go('/campaigns/c1/general/settings');
      await tester.pumpAndSettle();
      await _tapKey(tester, 'campaign-pack-$_packId');
      await _tapKey(tester, 'campaign-packs-save');

      router.go('/campaigns/c1/characters/new');
      await tester.pumpAndSettle();
      await toClassStep();
      expect(find.byKey(const Key('class-fighter')), findsOneWidget);
      expect(find.byKey(Key('class-${_alchemist.index}')), findsOneWidget);

      await _tapKey(tester, 'class-${_alchemist.index}');
      await _tapKey(tester, 'wizard-next');
      expect(find.byKey(const Key('step-class')), findsNothing);
    });
  });

  group('ficha', () {
    Map<String, dynamic> invalidJson() => {
      'replaceKey': 'replace.reinos-ejemplo-dote',
      'classIndex': null,
      'key': 'origin-feat',
      'level': 1,
      'setId': 'feats',
      'item': {'index': 'reinos-ejemplo-dote', 'name': 'Dote de ejemplo'},
      'reason': '',
      'code': 'pack-disabled',
    };

    testWidgets('una elección de un paquete desactivado lo explica', (tester) async {
      final characters = FakeCharactersRepository(
        characters: [
          makeCharacterJson(status: 'Active', invalidChoices: [invalidJson()]),
        ],
      );
      characters.invalidPlans['ch1'] = {
        'characterId': 'ch1',
        'invalid': [invalidJson()],
        'choices': <Object>[],
      };
      final (packs: _, catalog: _, :router) = await _pump(
        tester,
        location: '/characters/ch1',
        role: CampaignRole.dm,
        characters: characters,
      );

      expect(find.text('Hay elecciones de un paquete desactivado'), findsOneWidget);
      expect(
        find.text(
          'Dote de ejemplo: Este contenido pertenece a un paquete desactivado en la campaña.',
        ),
        findsOneWidget,
      );

      router.push('/characters/ch1/invalid-choices');
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('invalid-choices-intro'))).data,
        startsWith('El DM ha desactivado el paquete de contenido'),
      );
    });
  });
}
