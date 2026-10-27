/// Configuração por ambiente, via `--dart-define`.
///
/// Em desenvolvimento o emulador Android acessa a máquina host por `10.0.2.2` e usa o endpoint HTTP do
/// Gateway (o emulador não confia no certificado de desenvolvimento do .NET). Em produção:
/// `flutter run --dart-define=GATEWAY_URL=https://api.semogly.com --dart-define=KEYCLOAK_URL=https://id.semogly.com`.
abstract final class AppConfig {
  static const gatewayUrl = String.fromEnvironment('GATEWAY_URL', defaultValue: 'http://10.0.2.2:5100');

  /// Prefixo das rotas dos serviços no Gateway (YARP).
  static String get apiBaseUrl => '$gatewayUrl/api';

  /// Keycloak. Em desenvolvimento é `localhost` (com `adb reverse tcp:8080 tcp:8080`), não `10.0.2.2`: o endereço
  /// precisa ser o emissor dos tokens, o mesmo para o navegador do aparelho e para o host.
  static const keycloakUrl = String.fromEnvironment('KEYCLOAK_URL', defaultValue: 'http://localhost:8080');

  static String get issuer => '$keycloakUrl/realms/semogly-wallet';

  /// Cliente público do realm: Authorization Code + PKCE, sem segredo no app.
  static const clientId = 'wallet-mobile';

  /// Retorno do login ao app (esquema registrado no Android em manifestPlaceholders e no realm).
  static const redirectUrl = 'com.semogly.wallet:/oauthredirect';
}
