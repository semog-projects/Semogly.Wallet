import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/accounts/presentation/account_page.dart';
import '../features/auth/presentation/login_page.dart';
import '../features/auth/presentation/session_controller.dart';
import '../features/home/presentation/home_page.dart';
import '../features/transfers/presentation/notifications_page.dart';
import '../features/transfers/presentation/transfer_page.dart';
import '../features/transfers/presentation/transfer_status_page.dart';

abstract final class Routes {
  static const splash = '/splash';
  static const login = '/login';
  static const home = '/';
}

/// Rotas protegidas por sessão: o redirect reage a cada mudança do [sessionControllerProvider].
final routerProvider = Provider<GoRouter>((ref) {
  final refresh = ValueNotifier(0);
  ref.listen(sessionControllerProvider, (_, _) => refresh.value++);
  ref.onDispose(refresh.dispose);

  return GoRouter(
    initialLocation: Routes.splash,
    refreshListenable: refresh,
    redirect: (context, state) {
      final session = ref.read(sessionControllerProvider);
      final location = state.matchedLocation;

      // Restaurando a sessão do armazenamento seguro
      if (session.isLoading && !session.hasValue) {
        return location == Routes.splash ? null : Routes.splash;
      }

      final signedIn = session.value != null;
      if (!signedIn) {
        return location == Routes.login ? null : Routes.login;
      }

      return location == Routes.login || location == Routes.splash ? Routes.home : null;
    },
    routes: [
      GoRoute(
        path: Routes.splash,
        builder: (_, _) => const Scaffold(body: Center(child: CircularProgressIndicator())),
      ),
      GoRoute(path: Routes.login, builder: (_, _) => const LoginPage()),
      GoRoute(path: Routes.home, builder: (_, _) => const HomePage()),
      GoRoute(
        path: '/accounts/:id',
        builder: (_, state) => AccountPage(accountId: state.pathParameters['id']!),
        routes: [
          GoRoute(
            path: 'transfer',
            builder: (_, state) => TransferPage(sourceAccountId: state.pathParameters['id']!),
          ),
          GoRoute(
            path: 'notifications',
            builder: (_, state) => NotificationsPage(accountId: state.pathParameters['id']!),
          ),
        ],
      ),
      GoRoute(
        path: '/transfers/:transferId',
        builder: (_, state) => TransferStatusPage(
          args: (
            transferId: state.pathParameters['transferId']!,
            accountId: state.uri.queryParameters['account']!,
            amount: double.parse(state.uri.queryParameters['amount']!),
          ),
        ),
      ),
    ],
  );
});
