import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/dice/domain/dice_expression.dart';
import '../../features/items/data/models.dart';
import '../catalog/catalog_models.dart';
import '../characters/change_detail.dart';
import '../characters/models.dart';
import '../realtime/realtime_events.dart';
import '../theme/icons.dart';
import '../ui/breakdown.dart';
import 'game_system_ui.dart';

/// "Esta app no incluye el sistema X": what the app shows for a campaign of a
/// game system it does not bring. The sheet, the compendium and the DM table
/// show the notice; the rest of the campaign (lore, maps, sessions, shops...)
/// works as usual.
class UnsupportedSystemUi extends GameSystemUi {
  const UnsupportedSystemUi(this.id);

  @override
  final String id;

  @override
  String get name => id;

  /// "Esta app no incluye el sistema [id]."
  String get notice => 'Esta app no incluye el sistema $id.';

  Widget _notice() => UnsupportedSystemNotice(systemId: id);

  @override
  List<SystemAttribution> get attributions => const [];

  @override
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey) => const [];

  @override
  Widget combatView(
    BuildContext context,
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
    Widget? header,
  }) => _notice();

  @override
  List<SheetTab> detailTabs(
    CharacterDetail character, {
    required bool canEdit,
    bool isDm = false,
  }) => [SheetTab(id: 'sheet', label: 'Hoja', builder: (_) => _notice())];

  @override
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character) => null;

  @override
  Widget? sheetEditorSection(SheetEditorScope scope) => _notice();

  @override
  Widget? heightWeightRoller(HeightWeightScope scope) => null;

  @override
  Future<void> openCreationWizard(
    BuildContext context,
    String campaignId, {
    String? ownerUserId,
  }) async {
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(notice)));
  }

  @override
  List<CompendiumTab> compendiumTabs() => [
    CompendiumTab(id: 'unsupported', label: name, builder: (_) => _notice()),
  ];

  @override
  Future<List<CatalogSource>> catalogSources(Ref ref) async => const [];

  @override
  Widget itemExtras(EffectiveItem item) => const SizedBox.shrink();

  @override
  Widget? itemFormSection(ItemFormScope scope) => null;

  @override
  bool isCombatUsable(EffectiveItem item, {bool hasCharges = false}) =>
      hasCharges || item.isConsumable;

  @override
  Future<bool> openAttunement(BuildContext context, String characterId, CharacterItem item) async =>
      false;

  @override
  Widget partyPanel(BuildContext context, String campaignId) => _notice();

  @override
  String rosterSubtitle(CharacterSummary summary) => '';

  @override
  ChangeDetail? describeChangeRequest(ChangeRequest request) => null;

  @override
  String? staleScope(String campaignId) => null;

  @override
  RollClass classifyRoll(DiceResult result) => RollClass.normal;

  @override
  String formatMoney(int minorUnits) => '$minorUnits';

  @override
  AppIcons? breakdownIcon(BreakdownPart part) => null;

  @override
  void onRealtimeEvent(Ref ref, UnknownCampaignEvent event) {}
}

/// The notice of [UnsupportedSystemUi]: "Esta app no incluye el sistema X".
class UnsupportedSystemNotice extends StatelessWidget {
  const UnsupportedSystemNotice({super.key, required this.systemId});

  final String systemId;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Text(
        'Esta app no incluye el sistema $systemId.',
        key: const Key('unsupported-system'),
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyLarge,
      ),
    ),
  );
}
