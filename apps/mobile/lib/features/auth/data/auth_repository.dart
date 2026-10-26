import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_providers.dart';
import '../../../core/auth/jwt.dart';
import '../../../core/auth/token_storage.dart';
import '../../../core/config/app_config.dart';
import '../domain/session.dart';

/// Autenticação do app mobile: Bearer token (o BFF com cookie é exclusivo do web).
abstract interface class AuthRepository {
  /// Sessão salva no aparelho, se o token ainda for válido.
  Future<Session?> restore();

  Future<Session> signIn(String username);

  Future<void> signOut();
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => GatewayAuthRepository(ref.watch(tokenStorageProvider), Dio(BaseOptions(baseUrl: AppConfig.gatewayUrl))),
);

final class GatewayAuthRepository implements AuthRepository {
  GatewayAuthRepository(this._storage, this._dio);

  final TokenStorage _storage;
  final Dio _dio;

  @override
  Future<Session?> restore() async {
    final token = await _storage.read();
    final claims = token == null ? null : JwtClaims.tryParse(token);

    if (claims == null || claims.isExpired(DateTime.now().toUtc())) {
      await _storage.clear();
      return null;
    }

    return Session(username: claims.subject);
  }

  /// Emissor de desenvolvimento do Gateway. Em produção: fluxo OAuth2 Authorization Code + PKCE
  /// com o Identity Provider (ex.: flutter_appauth).
  @override
  Future<Session> signIn(String username) async {
    final response = await _dio.post<Map<String, dynamic>>('/auth/token', data: {'username': username});
    final token = response.data?['accessToken'] as String?;
    final claims = token == null ? null : JwtClaims.tryParse(token);

    if (token == null || claims == null) {
      throw StateError('Resposta de autenticação inválida.');
    }

    await _storage.write(token);
    return Session(username: claims.subject);
  }

  @override
  Future<void> signOut() => _storage.clear();
}
