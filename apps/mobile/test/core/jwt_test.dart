import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/auth/jwt.dart';

String tokenWith(Map<String, Object?> payload) {
  String encode(Map<String, Object?> json) => base64Url.encode(utf8.encode(jsonEncode(json))).replaceAll('=', '');
  return '${encode({'alg': 'HS256'})}.${encode(payload)}.assinatura';
}

void main() {
  test('usa o preferred_username do Keycloak (o sub é um id interno)', () {
    final claims = JwtClaims.tryParse(
      tokenWith({'sub': '9b2f7c1e-uuid', 'preferred_username': 'ana@email.com', 'exp': 1_800_000_000}),
    );

    expect(claims?.username, 'ana@email.com');
    expect(claims?.expiresAt, DateTime.utc(2027, 1, 15, 8));
  });

  test('identifica token expirado', () {
    final claims = JwtClaims.tryParse(tokenWith({'sub': 'ana', 'exp': 1_000}))!;

    expect(claims.isExpired(DateTime.now().toUtc()), isTrue);
  });

  test('retorna null para formatos inválidos', () {
    expect(JwtClaims.tryParse('abc'), isNull);
    expect(JwtClaims.tryParse('a.%%%.c'), isNull);
    expect(JwtClaims.tryParse(tokenWith({'exp': 1})), isNull);
    expect(JwtClaims.tryParse(tokenWith({'preferred_username': 'ana'})), isNull);
  });
}
