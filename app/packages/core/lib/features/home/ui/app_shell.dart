import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_icon.dart';
import '../../../core/theme/icons.dart';
import '../../campaigns/data/campaigns_controller.dart';

/// Branch indexes of the app shell, in the order of the bottom bar.
abstract final class AppBranches {
  static const campaigns = 0;
  static const compendium = 1;
  static const dice = 2;
  static const library = 3;
  static const profile = 4;
}

/// Frame of the signed-in app: a bottom bar with Campañas, Compendio, Dados,
/// Biblioteca and Perfil. Each branch keeps its own navigation stack; the
/// campaign screens open full screen on top of it.
///
/// The system back button returns to Campañas from the other tabs instead of
/// closing the app; on Campañas it leaves the app, as the main screen does.
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final invitations = ref.watch(myInvitationsControllerProvider).value?.length ?? 0;
    final onCampaigns = navigationShell.currentIndex == AppBranches.campaigns;
    return PopScope(
      canPop: onCampaigns,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) navigationShell.goBranch(AppBranches.campaigns);
      },
      child: Scaffold(
        body: navigationShell,
        bottomNavigationBar: NavigationBar(
          key: const Key('app-nav-bar'),
          selectedIndex: navigationShell.currentIndex,
          onDestinationSelected: (index) => navigationShell.goBranch(
            index,
            initialLocation: index == navigationShell.currentIndex,
          ),
          destinations: [
            NavigationDestination(
              key: const Key('nav-campaigns'),
              icon: Badge(
                key: const Key('nav-campaigns-badge'),
                isLabelVisible: invitations > 0,
                label: Text('$invitations'),
                child: const AppIcon(AppIcons.castle),
              ),
              label: 'Campañas',
            ),
            const NavigationDestination(
              key: Key('nav-compendium'),
              icon: AppIcon(AppIcons.book),
              label: 'Compendio',
            ),
            const NavigationDestination(
              key: Key('nav-dice'),
              icon: AppIcon(AppIcons.d20),
              label: 'Dados',
            ),
            const NavigationDestination(
              key: Key('nav-library'),
              icon: AppIcon(AppIcons.scroll),
              label: 'Biblioteca',
            ),
            const NavigationDestination(
              key: Key('nav-profile'),
              icon: AppIcon(AppIcons.hood),
              label: 'Perfil',
            ),
          ],
        ),
      ),
    );
  }
}
