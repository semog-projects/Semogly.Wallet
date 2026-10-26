/// Configuração por ambiente, via `--dart-define`.
///
/// Em desenvolvimento o emulador Android acessa a máquina host por `10.0.2.2` e usa o endpoint HTTP do
/// Gateway (o emulador não confia no certificado de desenvolvimento do .NET). Em produção:
/// `flutter run --dart-define=GATEWAY_URL=https://api.semogly.com`.
abstract final class AppConfig {
  static const gatewayUrl = String.fromEnvironment('GATEWAY_URL', defaultValue: 'http://10.0.2.2:5100');

  /// Prefixo das rotas dos serviços no Gateway (YARP).
  static String get apiBaseUrl => '$gatewayUrl/api';
}
