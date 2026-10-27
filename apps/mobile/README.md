# Semogly Wallet · App do cliente (Flutter)

App mobile do titular da conta (Android e iOS): login no Keycloak (OIDC com PKCE) e API pelo Gateway com Bearer token.

## Arquitetura

```
lib/
├── app/                 # MaterialApp, tema e rotas (go_router com redirect por sessão)
├── core/
│   ├── api/             # Dio + AuthInterceptor e clientes gerados do OpenAPI (generated/)
│   ├── auth/            # OIDC com o Keycloak, tokens no Keychain/Keystore e renovação (SessionTokens)
│   └── config/          # GATEWAY_URL e KEYCLOAK_URL via --dart-define
└── features/<feature>/
    ├── data/            # repositórios (implementações)
    ├── domain/          # modelos do app
    └── presentation/    # telas e controllers (Riverpod)
```

## Rodando

Com o AppHost no ar (`dotnet run --project src/Aspire/Semogly.Wallet.AppHost`) e um emulador Android aberto:

```bash
adb reverse tcp:8080 tcp:8080   # Keycloak do host como localhost:8080 no emulador
flutter run
```

O emulador acessa o Gateway em `http://10.0.2.2:5100` (o host da máquina). HTTP sem TLS é liberado **só no
build de debug** (`android/app/src/debug/res/xml/network_security_config.xml`); release exige HTTPS. Para outro
endereço: `flutter run --dart-define=GATEWAY_URL=https://... --dart-define=KEYCLOAK_URL=https://...`.

## Autenticação

- **Login:** Authorization Code + PKCE no navegador do sistema (`flutter_appauth`), cliente público `wallet-mobile`
  do realm; o app nunca vê a senha. O retorno é `com.semogly.wallet:/oauthredirect`.
- **Keycloak em `localhost`:** o endereço precisa ser o emissor dos tokens, o mesmo no navegador do aparelho e no
  host. O `adb reverse` leva a porta 8080 do emulador para o host (não use `10.0.2.2` aqui).
- **Tokens:** access e refresh token no armazenamento seguro. `SessionTokens` renova o access token pouco antes de
  expirar (uma renovação para chamadas simultâneas), para a API e o SignalR; se o Keycloak recusar, volta ao login.
- **Logout:** encerra a sessão no Keycloak pelo endpoint de logout (sem abrir o navegador) e apaga os tokens.
- **Android:** a `MainActivity` não usa `android:taskAffinity=""` (padrão do template): com ele, o retorno do
  login não traz o app de volta.

## Clientes da API

Gerados a partir do OpenAPI publicado pelo Gateway (`swagger_parser.yaml`), com o AppHost rodando:

```bash
dart run swagger_parser
dart run build_runner build -d
```

## Qualidade

`flutter analyze` (lints estritos, código gerado excluído) e `flutter test` rodam no CI.
