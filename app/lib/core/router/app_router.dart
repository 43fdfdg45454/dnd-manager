import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/admin/ui/admin_users_page.dart';
import '../../features/auth/ui/forgot_password_page.dart';
import '../../features/auth/ui/login_page.dart';
import '../../features/auth/ui/splash_page.dart';
import '../../features/campaigns/ui/campaign_detail_page.dart';
import '../../features/home/ui/home_page.dart';
import '../auth/auth_controller.dart';
import '../auth/auth_state.dart';

abstract final class AppRoutes {
  static const splash = '/splash';
  static const login = '/login';
  static const forgotPassword = '/forgot-password';
  static const home = '/';
  static const adminUsers = '/admin/users';
  static const campaignDetail = '/campaigns/:id';

  /// Location of the campaign with the given [id].
  static String campaign(String id) => '/campaigns/$id';
}

/// Computes the redirect target for [location] given the session [auth] state,
/// or null when the location is allowed.
String? authRedirect(AuthState auth, String location) {
  switch (auth) {
    case AuthUnknown():
      return location == AppRoutes.splash ? null : AppRoutes.splash;
    case AuthSignedOut():
      const open = {AppRoutes.login, AppRoutes.forgotPassword};
      return open.contains(location) ? null : AppRoutes.login;
    case AuthSignedIn(:final user):
      const guestOnly = {AppRoutes.splash, AppRoutes.login, AppRoutes.forgotPassword};
      if (guestOnly.contains(location)) return AppRoutes.home;
      if (location.startsWith('/admin') && !user.isAdmin) return AppRoutes.home;
      return null;
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  // Re-evaluates the redirect whenever the session state changes.
  final refresh = ValueNotifier<AuthState>(ref.read(authControllerProvider));
  ref.listen<AuthState>(authControllerProvider, (_, next) => refresh.value = next);

  final router = GoRouter(
    initialLocation: AppRoutes.home,
    refreshListenable: refresh,
    redirect: (context, state) => authRedirect(ref.read(authControllerProvider), state.uri.path),
    routes: [
      GoRoute(path: AppRoutes.splash, builder: (context, state) => const SplashPage()),
      GoRoute(path: AppRoutes.login, builder: (context, state) => const LoginPage()),
      GoRoute(
        path: AppRoutes.forgotPassword,
        builder: (context, state) => const ForgotPasswordPage(),
      ),
      GoRoute(path: AppRoutes.home, builder: (context, state) => const HomePage()),
      GoRoute(path: AppRoutes.adminUsers, builder: (context, state) => const AdminUsersPage()),
      GoRoute(
        path: AppRoutes.campaignDetail,
        builder: (context, state) => CampaignDetailPage(campaignId: state.pathParameters['id']!),
      ),
    ],
  );

  ref.onDispose(() {
    router.dispose();
    refresh.dispose();
  });
  return router;
});
