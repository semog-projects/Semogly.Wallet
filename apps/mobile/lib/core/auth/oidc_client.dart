import 'package:dio/dio.dart';
import 'package:flutter_appauth/flutter_appauth.dart';

import '../config/app_config.dart';
import 'token_storage.dart';

/// O usuário fechou a tela de login do Keycloak sem entrar.
final class SignInCancelledException implements Exception {
  const SignInCancelledException();
}

/// Conversa do app com o Keycloak (OpenID Connect).
abstract interface class OidcClient {
  /// Login no navegador do sistema: Authorization Code + PKCE.
  Future<AuthTokens> signIn();

  /// Novos tokens a partir do refresh token; null se a sessão no Keycloak expirou ou foi encerrada.
  Future<AuthTokens?> refresh(String refreshToken);

  /// Encerra a sessão no Keycloak (invalida o refresh token e o SSO do navegador).
  Future<void> endSession(String refreshToken);
}

final class KeycloakOidcClient implements OidcClient {
  KeycloakOidcClient({FlutterAppAuth? appAuth, Dio? dio})
    : _appAuth = appAuth ?? const FlutterAppAuth(),
      _dio = dio ?? Dio(BaseOptions(baseUrl: '${AppConfig.issuer}/protocol/openid-connect'));

  final FlutterAppAuth _appAuth;
  final Dio _dio;

  /// HTTP só no Keycloak local de desenvolvimento.
  static final _allowInsecure = AppConfig.keycloakUrl.startsWith('http://');

  @override
  Future<AuthTokens> signIn() async {
    try {
      final response = await _appAuth.authorizeAndExchangeCode(
        AuthorizationTokenRequest(
          AppConfig.clientId,
          AppConfig.redirectUrl,
          issuer: AppConfig.issuer,
          scopes: const ['openid', 'profile', 'email'],
          allowInsecureConnections: _allowInsecure,
        ),
      );

      final access = response.accessToken;
      final refresh = response.refreshToken;
      if (access == null || refresh == null) {
        throw StateError('O Keycloak não devolveu os tokens.');
      }

      return AuthTokens(accessToken: access, refreshToken: refresh);
    } on FlutterAppAuthUserCancelledException {
      throw const SignInCancelledException();
    }
  }

  @override
  Future<AuthTokens?> refresh(String refreshToken) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/token',
        data: {'grant_type': 'refresh_token', 'client_id': AppConfig.clientId, 'refresh_token': refreshToken},
        options: Options(contentType: Headers.formUrlEncodedContentType),
      );

      return AuthTokens(
        accessToken: response.data!['access_token'] as String,
        refreshToken: response.data!['refresh_token'] as String,
      );
    } on DioException catch (error) {
      // 400 invalid_grant: refresh token expirado ou sessão encerrada no Keycloak
      if (error.response?.statusCode == 400) {
        return null;
      }
      rethrow;
    }
  }

  @override
  Future<void> endSession(String refreshToken) async {
    // Pelo canal de servidor, sem abrir o navegador; o cliente público só precisa do client_id
    await _dio.post<void>(
      '/logout',
      data: {'client_id': AppConfig.clientId, 'refresh_token': refreshToken},
      options: Options(
        contentType: Headers.formUrlEncodedContentType,
        // Sessão já expirada no Keycloak: nada a encerrar
        validateStatus: (status) => status != null && (status < 300 || status == 400),
      ),
    );
  }
}
