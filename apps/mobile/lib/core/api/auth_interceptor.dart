import 'package:dio/dio.dart';

import '../auth/session_tokens.dart';

/// Anexa o Bearer token (renovado quando perto de expirar) em toda chamada à API e avisa quando o
/// Gateway recusa a sessão (401).
final class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokens, {required this.onUnauthorized});

  final SessionTokens _tokens;

  /// Chamado em 401: sessão expirada ou encerrada, precisa voltar ao login.
  final Future<void> Function() onUnauthorized;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    try {
      final token = await _tokens.accessToken();
      if (token != null) {
        options.headers['Authorization'] = 'Bearer $token';
      }
      handler.next(options);
    } on DioException catch (error) {
      // Falha de rede ao renovar o token: a requisição falha como qualquer erro de conexão
      handler.reject(error.copyWith(requestOptions: options));
    }
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode == 401) {
      await onUnauthorized();
    }
    handler.next(err);
  }
}
