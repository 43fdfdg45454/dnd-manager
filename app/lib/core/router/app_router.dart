import 'package:go_router/go_router.dart';

import '../../features/home/ui/home_page.dart';

final appRouter = GoRouter(
  routes: [
    GoRoute(path: '/', builder: (context, state) => const HomePage()),
  ],
);
