import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_providers.dart';
import '../../../core/auth/jwt.dart';
import '../../../core/auth/oidc_client.dart';
import '../../../core/auth/session_tokens.dart';
import '../../../core/auth/token_storage.dart';
import '../domain/session.dart';

/// Autenticação do app mobile: OIDC direto com o Keycloak e Bearer token na API (o BFF com cookie é do web).
abstract interface class AuthRepository {
  /// Sessão salva no aparelho, se ainda for válida (renova o access token se preciso).
  Future<Session?> restore();

  /// Abre a tela de login do Keycloak; lança [SignInCancelledException] se o usuário desistir.
  Future<Session> signIn();

  Future<void> signOut();
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => KeycloakAuthRepository(
    ref.watch(tokenStorageProvider),
    ref.watch(oidcClientProvider),
    ref.watch(sessionTokensProvider),
  ),
);

final class KeycloakAuthRepository implements AuthRepository {
  KeycloakAuthRepository(this._storage, this._oidc, this._tokens);

  final TokenStorage _storage;
  final OidcClient _oidc;
  final SessionTokens _tokens;

  @override
  Future<Session?> restore() async {
    final stored = await _storage.read();
    if (stored == null) {
      return null;
    }

    String? token;
    try {
      token = await _tokens.accessToken();
    } on DioException {
      // Sem rede para renovar: mantém a sessão; as chamadas à API dirão se ela ainda vale
      token = stored.accessToken;
    }

    return token == null ? null : _sessionFrom(token);
  }

  @override
  Future<Session> signIn() async {
    final tokens = await _oidc.signIn();
    final session = _sessionFrom(tokens.accessToken);

    if (session == null) {
      throw StateError('Token do Keycloak inválido.');
    }

    await _storage.write(tokens);
    return session;
  }

  @override
  Future<void> signOut() async {
    final stored = await _storage.read();
    await _storage.clear();

    if (stored != null) {
      try {
        await _oidc.endSession(stored.refreshToken);
      } on DioException {
        // Sem rede: os tokens já saíram do aparelho e a sessão no Keycloak expira sozinha
      }
    }
  }

  static Session? _sessionFrom(String accessToken) {
    final claims = JwtClaims.tryParse(accessToken);
    return claims == null ? null : Session(username: claims.username);
  }
}
