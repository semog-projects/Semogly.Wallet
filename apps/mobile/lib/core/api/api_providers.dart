import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/presentation/session_controller.dart';
import '../auth/oidc_client.dart';
import '../auth/session_tokens.dart';
import '../auth/token_storage.dart';
import '../config/app_config.dart';
import 'auth_interceptor.dart';
import 'generated/accounts/export.dart';
import 'generated/fraud/export.dart';
import 'generated/ledger/export.dart';
import 'generated/notifications/export.dart';
import 'generated/transfers/export.dart';

final tokenStorageProvider = Provider<TokenStorage>((ref) => SecureTokenStorage());

final oidcClientProvider = Provider<OidcClient>((ref) => KeycloakOidcClient());

/// Uma instância por app: concentra as renovações de token (chamadas à API e hub SignalR).
final sessionTokensProvider = Provider<SessionTokens>(
  (ref) => SessionTokens(ref.watch(tokenStorageProvider), ref.watch(oidcClientProvider)),
);

/// Dio da API: base no Gateway, Bearer automático (renovado pelo refresh token) e encerramento da sessão em 401.
final apiDioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 15),
    ),
  );

  dio.interceptors.add(
    AuthInterceptor(
      ref.watch(sessionTokensProvider),
      onUnauthorized: () => ref.read(sessionControllerProvider.notifier).signOut(),
    ),
  );

  return dio;
});

// Clientes gerados a partir do OpenAPI do Gateway (swagger_parser)
final accountsClientProvider = Provider((ref) => AccountsClient(ref.watch(apiDioProvider)));
final transfersClientProvider = Provider((ref) => TransfersClient(ref.watch(apiDioProvider)));
final ledgerClientProvider = Provider((ref) => LedgerClient(ref.watch(apiDioProvider)));
final fraudClientProvider = Provider((ref) => FraudClient(ref.watch(apiDioProvider)));
final notificationsClientProvider = Provider((ref) => NotificationsClient(ref.watch(apiDioProvider)));
