import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/theme/app_icon.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../../campaigns/ui/general/campaign_section_page.dart';
import '../../catalog/data/models.dart' show titleFromIndex;
import '../../dice/ui/dice_sheet.dart';
import '../../items/ui/inventory_tab.dart';
import '../data/characters_controller.dart';
import '../data/characters_repository.dart';
import '../data/models.dart';
import '../data/view_mode_controller.dart';
import '../domain/character_format.dart';
import '../domain/class_theme.dart';
import 'change_owner_dialog.dart';
import 'character_avatar.dart';
import 'character_tabs.dart';
import 'combat/combat_view.dart';

/// A character: a header with its actions and a tab bar whose first tab is
/// the combat view (Combate, Resumen, Habilidades, Rasgos, Hechizos,
/// Inventario, Notas). The last tab shown is remembered per character. A dice
/// button floats over every tab.
class CharacterPage extends ConsumerWidget {
  const CharacterPage({super.key, required this.characterId});

  final String characterId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(characterControllerProvider(characterId));
    return detail.when(
      skipLoadingOnReload: true,
      loading: () => Scaffold(
        appBar: AppBar(title: const Text('Personaje')),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: const Text('Personaje')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeCharacterError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(characterControllerProvider(characterId)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
      ),
      data: (character) => _CharacterView(character: character),
    );
  }
}

const _tabs = [
  Tab(key: Key('tab-combat'), text: 'Combate'),
  Tab(key: Key('tab-summary'), text: 'Resumen'),
  Tab(key: Key('tab-skills'), text: 'Habilidades'),
  Tab(key: Key('tab-traits'), text: 'Rasgos'),
  Tab(key: Key('tab-spells'), text: 'Hechizos'),
  Tab(key: Key('tab-inventory'), text: 'Inventario'),
  Tab(key: Key('tab-notes'), text: 'Notas'),
];

class _CharacterView extends ConsumerStatefulWidget {
  const _CharacterView({required this.character});

  final CharacterDetail character;

  @override
  ConsumerState<_CharacterView> createState() => _CharacterViewState();
}

class _CharacterViewState extends ConsumerState<_CharacterView>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;

  CharacterDetail get character => widget.character;

  @override
  void initState() {
    super.initState();
    final initial = ref.read(characterTabProvider(widget.character.id));
    _tabController = TabController(length: _tabs.length, initialIndex: initial.index, vsync: this)
      ..addListener(_rememberTab);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  /// Stores the tab once the change settles (not on every animation tick).
  void _rememberTab() {
    if (_tabController.indexIsChanging) return;
    ref
        .read(characterTabProvider(character.id).notifier)
        .select(CharacterTab.values[_tabController.index]);
  }

  CharacterController _controller(WidgetRef ref) =>
      ref.read(characterControllerProvider(character.id).notifier);

  Future<void> _delete(BuildContext context, WidgetRef ref) async {
    final confirmed = await confirmAction(
      context,
      title: 'Eliminar personaje',
      message: '¿Seguro que quieres eliminar a "${character.name}"? No se puede deshacer.',
      confirmLabel: 'Eliminar',
    );
    if (!confirmed || !context.mounted) return;
    final router = GoRouter.of(context);
    final done = await runAction(
      context,
      () => _controller(ref).delete(),
      success: 'Personaje eliminado.',
      describe: describeCharacterError,
    );
    if (!done) return;
    if (router.canPop()) {
      router.pop();
    } else {
      router.go(AppRoutes.campaignSection(character.campaignId, CampaignSection.characters));
    }
  }

  /// DM only: hands the character to a player or turns it into an NPC.
  Future<void> _changeOwner(BuildContext context, WidgetRef ref) async {
    final members =
        ref.read(campaignDetailControllerProvider(character.campaignId)).value?.members ?? const [];
    final picked = await pickCharacterOwner(
      context,
      characterName: character.name,
      members: members,
      currentOwnerUserId: character.ownerUserId,
    );
    if (picked == null || !context.mounted) return;
    final name = members.where((m) => m.userId == picked.userId).firstOrNull?.displayName;
    await runAction(
      context,
      () => _controller(ref).setOwner(picked.userId),
      success: name == null
          ? '${character.name} es ahora un PNJ.'
          : '${character.name} es ahora de $name.',
      describe: describeCharacterError,
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final campaign = ref.watch(campaignDetailControllerProvider(character.campaignId));
    final permissions = CharacterPermissions(
      character: character,
      myUserId: myUserId,
      myRole: campaign.value?.myRole,
    );

    return Scaffold(
      appBar: AppBar(
        title: const Text('Personaje'),
        actions: [
          if (permissions.canDelete || permissions.isDm)
            PopupMenuButton<String>(
              key: const Key('character-menu'),
              onSelected: (value) =>
                  value == 'owner' ? _changeOwner(context, ref) : _delete(context, ref),
              itemBuilder: (_) => [
                if (permissions.isDm)
                  const PopupMenuItem(
                    key: Key('character-menu-owner'),
                    value: 'owner',
                    child: Text('Cambiar jugador'),
                  ),
                if (permissions.canDelete)
                  const PopupMenuItem(value: 'delete', child: Text('Eliminar')),
              ],
            ),
        ],
        bottom: TabBar(
          key: const Key('character-tabs'),
          controller: _tabController,
          isScrollable: true,
          tabAlignment: TabAlignment.start,
          tabs: _tabs,
        ),
      ),
      floatingActionButton: FloatingActionButton(
        key: const Key('dice-fab'),
        tooltip: 'Dados',
        onPressed: () => showDiceSheet(context),
        child: const Icon(Icons.casino_outlined),
      ),
      body: OfflineBannerLayout(
        scopes: [staleTree(CharactersRepository.characterPath(character.id))],
        child: NestedScrollView(
          headerSliverBuilder: (context, _) => [
            SliverToBoxAdapter(
              child: _Header(
                character: character,
                permissions: permissions,
                onSubmit: () => runAction(
                  context,
                  () async {
                    await _controller(ref).submit();
                  },
                  success: 'Enviado al DM para aprobación',
                  describe: describeCharacterError,
                ),
                onActivate: () => runAction(
                  context,
                  () => _controller(ref).activate(),
                  success: 'Personaje activado.',
                  describe: describeCharacterError,
                ),
              ),
            ),
          ],
          body: TabBarView(
            controller: _tabController,
            children: [
              CombatView(
                character: character,
                canEdit: permissions.canEdit,
                isDm: permissions.isDm,
              ),
              SummaryTab(
                character: character,
                canEdit: permissions.canEdit,
                isDm: permissions.isDm,
              ),
              SkillsTab(character: character),
              TraitsTab(character: character),
              SpellsTab(character: character),
              InventoryTab(character: character),
              NotesTab(character: character),
            ],
          ),
        ),
      ),
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

class _Header extends StatelessWidget {
  const _Header({
    required this.character,
    required this.permissions,
    required this.onSubmit,
    required this.onActivate,
  });

  final CharacterDetail character;
  final CharacterPermissions permissions;
  final VoidCallback onSubmit;
  final VoidCallback onActivate;

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
    final pending = c.pendingChangeRequests.where((r) => r.isPending).length;

    return ClassAccent(
      classIndex: mainClassIndex(c),
      child: Builder(builder: (context) => _buildHeader(context, classes, pending)),
    );
  }

  Widget _buildHeader(BuildContext context, String classes, int pending) {
    final theme = Theme.of(context);
    final c = character;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CharacterAvatar(character: c, canEdit: permissions.canEdit),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      c.name,
                      key: const Key('character-title'),
                      style: theme.textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 2),
                    _ClassLine(
                      character: c,
                      child: Text(
                        '$_race · $classes${c.classes.isEmpty ? '' : ' · Nivel ${c.totalLevel}'}',
                        key: const Key('character-subtitle'),
                        style: theme.textTheme.bodyMedium,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: [
              Chip(
                key: const Key('character-status'),
                label: Text(c.status.label),
                visualDensity: VisualDensity.compact,
              ),
              if (c.missingContent.isNotEmpty) CatalogMissingBadge(character: c),
              if (pending > 0)
                ActionChip(
                  key: const Key('character-pending'),
                  avatar: const Icon(Icons.hourglass_top, size: 16),
                  label: Text(
                    pending == 1 ? '1 solicitud pendiente' : '$pending solicitudes pendientes',
                  ),
                  visualDensity: VisualDensity.compact,
                  onPressed: () => context.push(AppRoutes.changeRequests(c.campaignId)),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              if (permissions.canEdit)
                OfflineAware(
                  builder: (context, canWrite) => FilledButton.tonalIcon(
                    key: const Key('character-edit'),
                    onPressed: !canWrite ? null : () => context.push(AppRoutes.characterEdit(c.id)),
                    icon: const Icon(Icons.edit_outlined),
                    label: const Text('Editar hoja'),
                  ),
                ),
              if (permissions.canSubmit)
                FilledButton.icon(
                  key: const Key('character-submit'),
                  onPressed: onSubmit,
                  icon: const Icon(Icons.send_outlined),
                  label: const Text('Enviar al DM'),
                ),
              if (permissions.canActivate)
                FilledButton.icon(
                  key: const Key('character-activate'),
                  onPressed: onActivate,
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Activar'),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The subtitle of a header with the icon of the character's main class,
/// tinted with its accent.
class _ClassLine extends StatelessWidget {
  const _ClassLine({required this.character, required this.child});

  final CharacterDetail character;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final main = mainClassIndex(character);
    if (main == null) return child;
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
        Flexible(child: child),
      ],
    );
  }
}
