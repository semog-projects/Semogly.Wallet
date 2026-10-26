# Semogly Wallet · App do cliente (Flutter)

App mobile do titular da conta (Android e iOS), consumindo a API pelo Gateway com Bearer token.

## Arquitetura

```
lib/
├── app/                 # MaterialApp, tema e rotas (go_router com redirect por sessão)
├── core/
│   ├── api/             # Dio + AuthInterceptor e clientes gerados do OpenAPI (generated/)
│   ├── auth/            # TokenStorage (Keychain/Keystore) e leitura das claims do JWT
│   └── config/          # GATEWAY_URL via --dart-define
└── features/<feature>/
    ├── data/            # repositórios (implementações)
    ├── domain/          # modelos do app
    └── presentation/    # telas e controllers (Riverpod)
```

## Rodando

Com o AppHost no ar (`dotnet run --project src/Aspire/Semogly.Wallet.AppHost`) e um emulador Android aberto:

```bash
flutter run
```

O emulador acessa o Gateway em `http://10.0.2.2:5100` (o host da máquina). HTTP sem TLS é liberado **só no
build de debug** (`android/app/src/debug/res/xml/network_security_config.xml`); release exige HTTPS. Para outro
endereço: `flutter run --dart-define=GATEWAY_URL=https://...`.

## Clientes da API

Gerados a partir do OpenAPI publicado pelo Gateway (`swagger_parser.yaml`), com o AppHost rodando:

```bash
dart run swagger_parser
dart run build_runner build -d
```

## Qualidade

`flutter analyze` (lints estritos, código gerado excluído) e `flutter test` rodam no CI.
