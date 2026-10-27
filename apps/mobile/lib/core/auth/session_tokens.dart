import 'jwt.dart';
import 'oidc_client.dart';
import 'token_storage.dart';

/// Entrega um access token válido para as chamadas à API e ao hub, renovando-o com o refresh token
/// pouco antes de expirar. Renovações simultâneas viram uma só (o refresh token pode ser rotacionado).
final class SessionTokens {
  SessionTokens(this._storage, this._oidc, {DateTime Function()? clock}) : _clock = clock ?? DateTime.now;

  /// Renova com folga, para o token não vencer no caminho até o Gateway.
  static const refreshMargin = Duration(seconds: 30);

  final TokenStorage _storage;
  final OidcClient _oidc;
  final DateTime Function() _clock;

  Future<String?>? _refreshing;

  /// Access token válido; null sem sessão ou se a sessão no Keycloak acabou (os tokens são descartados).
  /// Erros de rede na renovação são propagados: a sessão continua salva para a próxima tentativa.
  Future<String?> accessToken() async {
    final tokens = await _storage.read();
    if (tokens == null) {
      return null;
    }

    final claims = JwtClaims.tryParse(tokens.accessToken);
    if (claims != null && claims.expiresAt.isAfter(_clock().toUtc().add(refreshMargin))) {
      return tokens.accessToken;
    }

    return _refreshing ??= _refresh(tokens.refreshToken).whenComplete(() => _refreshing = null);
  }

  Future<String?> _refresh(String refreshToken) async {
    final renewed = await _oidc.refresh(refreshToken);

    if (renewed == null) {
      await _storage.clear();
      return null;
    }

    await _storage.write(renewed);
    return renewed.accessToken;
  }
}
