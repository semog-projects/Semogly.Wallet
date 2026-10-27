import 'dart:convert';

/// Claims do JWT lidas no app apenas para exibição. A validação da assinatura é sempre do Gateway.
final class JwtClaims {
  const JwtClaims({required this.username, required this.expiresAt});

  /// Nome de usuário do Keycloak (preferred_username; para clientes, o e-mail). O "sub" é um id interno.
  final String username;
  final DateTime expiresAt;

  bool isExpired(DateTime now) => !now.isBefore(expiresAt);

  /// Lê o payload (segunda parte do token); null se o formato for inválido.
  static JwtClaims? tryParse(String token) {
    final parts = token.split('.');
    if (parts.length != 3) {
      return null;
    }

    try {
      final payload = jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))));
      if (payload is! Map<String, dynamic> || payload['exp'] is! int) {
        return null;
      }

      final username = payload['preferred_username'] ?? payload['email'] ?? payload['sub'];
      if (username is! String) {
        return null;
      }

      return JwtClaims(
        username: username,
        expiresAt: DateTime.fromMillisecondsSinceEpoch((payload['exp'] as int) * 1000, isUtc: true),
      );
    } on FormatException {
      return null;
    }
  }
}
