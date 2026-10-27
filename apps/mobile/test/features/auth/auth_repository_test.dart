import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/auth/session_tokens.dart';
import 'package:semogly_wallet/core/auth/token_storage.dart';
import 'package:semogly_wallet/features/auth/data/auth_repository.dart';

import '../../support/fakes.dart';

void main() {
  final valid = jwt(
    username: 'maria.demo@email.com',
    expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 10)),
  );
  final expired = jwt(
    username: 'maria.demo@email.com',
    expiresAt: DateTime.now().toUtc().subtract(const Duration(hours: 1)),
  );

  KeycloakAuthRepository repositoryWith(TokenStorage storage, FakeOidcClient oidc) =>
      KeycloakAuthRepository(storage, oidc, SessionTokens(storage, oidc));

  test('login guarda access e refresh token e abre a sessão com o usuário do Keycloak', () async {
    final storage = InMemoryTokenStorage();
    final oidc = FakeOidcClient(
      signInTokens: AuthTokens(accessToken: valid, refreshToken: 'r1'),
    );

    final session = await repositoryWith(storage, oidc).signIn();

    expect(session.username, 'maria.demo@email.com');
    expect((await storage.read())?.refreshToken, 'r1');
  });

  test('ao abrir o app, renova o access token expirado com o refresh token', () async {
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));
    final oidc = FakeOidcClient(
      renewed: AuthTokens(accessToken: valid, refreshToken: 'r2'),
    );

    final session = await repositoryWith(storage, oidc).restore();

    expect(session?.username, 'maria.demo@email.com');
    expect((await storage.read())?.accessToken, valid);
  });

  test('ao abrir o app, sessão expirada no Keycloak leva ao login', () async {
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));

    expect(await repositoryWith(storage, FakeOidcClient()).restore(), isNull);
    expect(await storage.read(), isNull);
  });

  test('ao abrir o app sem rede, mantém a sessão salva', () async {
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));

    final session = await repositoryWith(storage, FakeOidcClient(offline: true)).restore();

    expect(session?.username, 'maria.demo@email.com');
  });

  test('sair encerra a sessão no Keycloak e apaga os tokens do aparelho', () async {
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: valid, refreshToken: 'r1'));
    final oidc = FakeOidcClient();

    await repositoryWith(storage, oidc).signOut();

    expect(oidc.endedSessions, ['r1']);
    expect(await storage.read(), isNull);
  });

  test('sair sem rede ainda apaga os tokens do aparelho', () async {
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: valid, refreshToken: 'r1'));

    await repositoryWith(storage, FakeOidcClient(offline: true)).signOut();

    expect(await storage.read(), isNull);
  });
}
