import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/api/auth_interceptor.dart';
import 'package:semogly_wallet/core/auth/token_storage.dart';

/// Responde com o status configurado e guarda a última requisição recebida.
final class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.statusCode);

  final int statusCode;
  RequestOptions? lastRequest;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    lastRequest = options;
    return ResponseBody.fromString(jsonEncode({}), statusCode, headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    });
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  Dio dioWith(FakeAdapter adapter, TokenStorage storage, Future<void> Function() onUnauthorized) =>
      Dio(BaseOptions(baseUrl: 'http://gateway/api'))
        ..httpClientAdapter = adapter
        ..interceptors.add(AuthInterceptor(storage, onUnauthorized: onUnauthorized));

  test('anexa o Bearer token salvo', () async {
    final adapter = FakeAdapter(200);

    await dioWith(adapter, InMemoryTokenStorage('token-123'), () async {}).get<void>('/accounts/1');

    expect(adapter.lastRequest?.headers['Authorization'], 'Bearer token-123');
  });

  test('sem token, não envia Authorization', () async {
    final adapter = FakeAdapter(200);

    await dioWith(adapter, InMemoryTokenStorage(), () async {}).get<void>('/accounts/1');

    expect(adapter.lastRequest?.headers.containsKey('Authorization'), isFalse);
  });

  test('401 do Gateway encerra a sessão', () async {
    var signedOut = false;
    final dio = dioWith(FakeAdapter(401), InMemoryTokenStorage('expirado'), () async => signedOut = true);

    await expectLater(dio.get<void>('/accounts/1'), throwsA(isA<DioException>()));
    expect(signedOut, isTrue);
  });
}
