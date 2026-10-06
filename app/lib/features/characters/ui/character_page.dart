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
import '../../catalog/data/models.dart' show titleFromIndex;
import '../../dice/ui/dice_sheet.dart';
import '../../items/ui/inventory_tab.dart';
import '../data/characters_controller.dart';
import '../data/characters_repository.dart';
import '../data/models.dart';
import '../data/view_mode_controller.dart';
import '../domain/character_format.dart';
import '../domain/class_theme.dart';
import 'character_avatar.dart';
import 'character_tabs.dart';
import 'combat/combat_view.dart';

/// A character: the detailed view (header with actions and the sheet tabs) or
/// the combat view, chosen with the switch of the header and remembered per
/// character. A dice button floats over both.
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
  Tab(key: Key('tab-summary'), text: 'Resumen'),
  Tab(key: Key('tab-skills'), text: 'Habilidades'),
  Tab(key: Key('tab-traits'), text: 'Rasgos'),
  Tab(key: Key('tab-spells'), text: 'Hechizos'),
  Tab(key: Key('tab-inventory'), text: 'Inventario'),
  Tab(key: Key('tab-notes'), text: 'Notas'),
];

class _CharacterView extends ConsumerWidget {
  const _CharacterView({required this.character});

  final CharacterDetail character;

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
      router.go(AppRoutes.campaign(character.campaignId));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final myUserId = auth is AuthSignedIn ? auth.user.id : '';
    final campaign = ref.watch(campaignDetailControllerProvider(character.campaignId));
    final permissions = CharacterPermissions(
      character: character,
      myUserId: myUserId,
      myRole: campaign.value?.myRole,
    );

    final combat = ref.watch(characterViewModeProvider(character.id)) == CharacterViewMode.combat;
    final switcher = _ViewSwitch(
      combat: combat,
      onChanged: (next) => ref.read(characterViewModeProvider(character.id).notifier).select(next),
    );

    return DefaultTabController(
      length: _tabs.length,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Personaje'),
          actions: [
            if (permissions.canDelete)
              PopupMenuButton<String>(
                key: const Key('character-menu'),
                onSelected: (_) => _delete(context, ref),
                itemBuilder: (_) => const [PopupMenuItem(value: 'delete', child: Text('Eliminar'))],
              ),
          ],
          bottom: combat
              ? null
              : const TabBar(isScrollable: true, tabAlignment: TabAlignment.start, tabs: _tabs),
        ),
        floatingActionButton: FloatingActionButton(
          key: const Key('dice-fab'),
          tooltip: 'Dados',
          onPressed: () => showDiceSheet(context),
          child: const Icon(Icons.casino_outlined),
        ),
        body: OfflineBannerLayout(
          scopes: [staleTree(CharactersRepository.characterPath(character.id))],
          child: combat
              ? CombatView(
                  character: character,
                  canEdit: permissions.canEdit,
                  header: _CombatHeader(character: character, switcher: switcher),
                )
              : NestedScrollView(
                  headerSliverBuilder: (context, _) => [
                    SliverToBoxAdapter(
                      child: _Header(
                        character: character,
                        permissions: permissions,
                        switcher: switcher,
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
                    children: [
                      SummaryTab(character: character),
                      SkillsTab(character: character),
                      TraitsTab(character: character),
                      SpellsTab(character: character),
                      InventoryTab(character: character),
                      NotesTab(character: character),
                    ],
                  ),
                ),
        ),
      ),
    );
  }
}

/// The Detallado / Combate switch.
class _ViewSwitch extends StatelessWidget {
  const _ViewSwitch({required this.combat, required this.onChanged});

  final bool combat;
  final ValueChanged<CharacterViewMode> onChanged;

  @override
  Widget build(BuildContext context) {
    return SegmentedButton<CharacterViewMode>(
      key: const Key('view-mode'),
      showSelectedIcon: false,
      segments: const [
        ButtonSegment(value: CharacterViewMode.detailed, label: Text('Detallado')),
        ButtonSegment(value: CharacterViewMode.combat, label: Text('Combate')),
      ],
      selected: {combat ? CharacterViewMode.combat : CharacterViewMode.detailed},
      onSelectionChanged: (selection) => onChanged(selection.first),
    );
  }
}

/// Compact header of the combat view: name, class summary and the view switch.
class _CombatHeader extends StatelessWidget {
  const _CombatHeader({required this.character, required this.switcher});

  final CharacterDetail character;
  final Widget switcher;

  @override
  Widget build(BuildContext context) {
    final c = character;
    return ClassAccent(
      classIndex: mainClassIndex(c),
      child: Builder(builder: (context) => _buildHeader(context)),
    );
  }

  Widget _buildHeader(BuildContext context) {
    final theme = Theme.of(context);
    final c = character;
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CharacterAvatar(character: c, radius: 24),
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
                    if (c.classes.isNotEmpty)
                      _ClassLine(
                        character: c,
                        child: Text(
                          '${classesLabel(c.classes)} · Nivel ${c.totalLevel}',
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
          switcher,
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({
    required this.character,
    required this.permissions,
    required this.switcher,
    required this.onSubmit,
    required this.onActivate,
  });

  final CharacterDetail character;
  final CharacterPermissions permissions;
  final Widget switcher;
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
          const SizedBox(height: 12),
          switcher,
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
