import 'package:dio/dio.dart';

import '../auth/token_storage.dart';

/// Anexa o Bearer token em toda chamada à API e avisa quando o Gateway recusa a sessão (401).
final class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._storage, {required this.onUnauthorized});

  final TokenStorage _storage;

  /// Chamado em 401: token expirado ou inválido, a sessão precisa ser encerrada.
  final Future<void> Function() onUnauthorized;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    final token = await _storage.read();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode == 401) {
      await onUnauthorized();
    }
    handler.next(err);
  }
}
