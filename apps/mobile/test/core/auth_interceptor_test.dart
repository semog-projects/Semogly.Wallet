import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/api/auth_interceptor.dart';
import 'package:semogly_wallet/core/auth/session_tokens.dart';
import 'package:semogly_wallet/core/auth/token_storage.dart';

import '../support/fakes.dart';

/// Responde com o status configurado e guarda as requisições recebidas.
final class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.statusCode);

  final int statusCode;
  final requests = <RequestOptions>[];

  RequestOptions? get lastRequest => requests.lastOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return ResponseBody.fromString(
      jsonEncode({}),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  final valid = jwt(expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 10)));
  final expired = jwt(expiresAt: DateTime.now().toUtc().subtract(const Duration(minutes: 1)));
  final renewed = jwt(expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 30)));

  Dio dioWith(FakeAdapter adapter, SessionTokens tokens, [Future<void> Function()? onUnauthorized]) =>
      Dio(BaseOptions(baseUrl: 'http://gateway/api'))
        ..httpClientAdapter = adapter
        ..interceptors.add(AuthInterceptor(tokens, onUnauthorized: onUnauthorized ?? () async {}));

  test('anexa o access token salvo enquanto ele é válido', () async {
    final adapter = FakeAdapter(200);
    final oidc = FakeOidcClient();
    final tokens = SessionTokens(InMemoryTokenStorage(AuthTokens(accessToken: valid, refreshToken: 'r1')), oidc);

    await dioWith(adapter, tokens).get<void>('/accounts/1');

    expect(adapter.lastRequest?.headers['Authorization'], 'Bearer $valid');
    expect(oidc.refreshedWith, isEmpty);
  });

  test('token expirado é renovado antes da chamada e o novo par é salvo', () async {
    final adapter = FakeAdapter(200);
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));
    final oidc = FakeOidcClient(
      renewed: AuthTokens(accessToken: renewed, refreshToken: 'r2'),
    );

    await dioWith(adapter, SessionTokens(storage, oidc)).get<void>('/accounts/1');

    expect(adapter.lastRequest?.headers['Authorization'], 'Bearer $renewed');
    expect(oidc.refreshedWith, ['r1']);
    expect((await storage.read())?.refreshToken, 'r2');
  });

  test('chamadas simultâneas com token expirado renovam uma única vez', () async {
    final adapter = FakeAdapter(200);
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));
    final oidc = FakeOidcClient(
      renewed: AuthTokens(accessToken: renewed, refreshToken: 'r2'),
    );
    final dio = dioWith(adapter, SessionTokens(storage, oidc));

    await Future.wait([dio.get<void>('/a'), dio.get<void>('/b'), dio.get<void>('/c')]);

    expect(oidc.refreshedWith, ['r1']);
    expect(adapter.requests.map((r) => r.headers['Authorization']), everyElement('Bearer $renewed'));
  });

  test('sessão encerrada no Keycloak descarta os tokens e o 401 encerra a sessão do app', () async {
    var signedOut = false;
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));
    final dio = dioWith(FakeAdapter(401), SessionTokens(storage, FakeOidcClient()), () async => signedOut = true);

    await expectLater(dio.get<void>('/accounts/1'), throwsA(isA<DioException>()));

    expect(await storage.read(), isNull);
    expect(signedOut, isTrue);
  });

  test('sem rede para renovar, a chamada falha mas a sessão continua salva', () async {
    final adapter = FakeAdapter(200);
    final storage = InMemoryTokenStorage(AuthTokens(accessToken: expired, refreshToken: 'r1'));
    final dio = dioWith(adapter, SessionTokens(storage, FakeOidcClient(offline: true)));

    await expectLater(
      dio.get<void>('/accounts/1'),
      throwsA(isA<DioException>().having((e) => e.type, 'type', DioExceptionType.connectionError)),
    );

    expect(adapter.requests, isEmpty);
    expect((await storage.read())?.refreshToken, 'r1');
  });

  test('sem sessão, não envia Authorization', () async {
    final adapter = FakeAdapter(200);

    await dioWith(adapter, SessionTokens(InMemoryTokenStorage(), FakeOidcClient())).get<void>('/accounts/1');

    expect(adapter.lastRequest?.headers.containsKey('Authorization'), isFalse);
  });
}
