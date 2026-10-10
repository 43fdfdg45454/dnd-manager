import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/characters/models.dart';
import '../../../core/cache/stale_data.dart';
import '../../../core/network/api_error.dart';
import '../../../core/router/app_router.dart';
import '../../../core/systems/game_system_ui.dart';
import '../../../core/systems/system_registry.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../campaigns/data/campaigns_controller.dart';
import '../../campaigns/ui/confirm_dialog.dart';
import '../../campaigns/ui/feedback.dart';
import '../../campaigns/ui/general/campaign_section_page.dart';
import '../../dice/ui/dice_sheet.dart';
import '../data/characters_controller.dart';
import '../data/characters_repository.dart';
import '../data/view_mode_controller.dart';
import '../domain/character_permissions.dart';
import 'change_owner_dialog.dart';
import 'character_avatar.dart';
import 'character_detail_tabs.dart';

/// A character: a header with its actions and two main tabs, "Combate" (the
/// combat view of its game system) and "Detalle" ([CharacterDetailTabs]: the
/// sub-tabs of the system, Inventario and Notas). The main view and the last sub-tab
/// are remembered per character. A dice button floats over every tab.
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

/// The main tabs, in the order of [CharacterView].
const _tabs = [
  Tab(key: Key('tab-combat'), text: 'Combate'),
  Tab(key: Key('tab-detail'), text: 'Detalle'),
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
    final initial = ref.read(characterViewProvider(widget.character.id));
    _tabController = TabController(length: _tabs.length, initialIndex: initial.index, vsync: this)
      ..addListener(_rememberView);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  /// Stores the main view once the change settles (not on every animation tick).
  void _rememberView() {
    if (_tabController.indexIsChanging) return;
    ref
        .read(characterViewProvider(character.id).notifier)
        .select(CharacterView.values[_tabController.index]);
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
    final system = ref.watch(campaignSystemUiProvider(character.campaignId));

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
        bottom: TabBar(key: const Key('character-tabs'), controller: _tabController, tabs: _tabs),
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
                system: system,
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
              system.combatView(
                context,
                character,
                canEdit: permissions.canEdit,
                isDm: permissions.isDm,
              ),
              CharacterDetailTabs(character: character, permissions: permissions),
            ],
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({
    required this.system,
    required this.character,
    required this.permissions,
    required this.onSubmit,
    required this.onActivate,
  });

  final GameSystemUi system;
  final CharacterDetail character;
  final CharacterPermissions permissions;
  final VoidCallback onSubmit;
  final VoidCallback onActivate;

  @override
  Widget build(BuildContext context) {
    final c = character;
    final pending = c.pendingChangeRequests.where((r) => r.isPending).length;

    return system.characterAccent(
      c,
      child: Builder(builder: (context) => _buildHeader(context, pending)),
    );
  }

  Widget _buildHeader(BuildContext context, int pending) {
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
                    if (system.characterHeadline(c) case final headline?) ...[
                      const SizedBox(height: 2),
                      headline,
                    ],
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
              ...system.characterBadges(c),
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
