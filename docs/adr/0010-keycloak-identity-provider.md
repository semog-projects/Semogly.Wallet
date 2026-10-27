# ADR 0010: Keycloak como Identity Provider

**Status:** aceita (substitui o emissor de tokens de desenvolvimento da [ADR 0008](0008-gateway-yarp-jwt.md))

## Contexto

O Gateway emitia JWTs HS256 para qualquer usuário informado, sem senha, com uma chave simétrica compartilhada com
o Accounts. Isso bastava para o MVP, mas não autentica ninguém de verdade, espalha a chave de assinatura e não
oferece os fluxos que o web e o app precisam (OIDC com redirecionamento e PKCE).

## Decisão

- **Keycloak 26** como Identity Provider, orquestrado pelo Aspire (`Aspire.Hosting.Keycloak`, ainda em preview):
  - O realm `semogly-wallet` fica versionado em `src/Aspire/Semogly.Wallet.AppHost/Realms` e é importado na subida.
  - Clientes: `wallet-gateway` (confidencial, usado pelo BFF e pelo Gateway) e `wallet-mobile` (público, PKCE).
  - Papéis de realm `customer` e `operator`, emitidos na claim plana `role`; audiência `semogly-wallet-api`.
- **Issuer estável:** endpoint HTTPS fixo em `https://localhost:8443` (certificado de desenvolvimento do Aspire,
  publicado direto pelo Docker) com `KC_HOSTNAME`. Tokens obtidos pelo navegador, pelo app ou pelo Gateway
  (via service discovery) têm o mesmo `iss`, e `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` mantém o back-channel interno.
- **Validação:** `AddWalletJwtBearer` no ServiceDefaults usa `AddKeycloakJwtBearer` (metadados e JWKS pelo
  service discovery), valida a audiência e mapeia `preferred_username` e `role`. O Gateway e o Accounts usam a
  mesma configuração; não há mais chave compartilhada.
- **Login em Development** (transição até as issues #40 e #41): `POST /auth/token` e `POST /bff/login` fazem
  password grant no cliente `wallet-gateway` e respondem 401 para credenciais inválidas.
- **Sessão do BFF no servidor:** os tokens RS256 do Keycloak deixariam o cookie grande demais (fatiado em vários
  `Set-Cookie`). O ticket fica no Redis, protegido pelo Data Protection, e o cookie carrega só a chave da sessão.
- **Testes de integração:** `Testcontainers.Keycloak` importa o mesmo arquivo de realm; usuários são criados pela
  API de administração e os testes usam tokens reais.

## Consequências

- **Positivas:**
  - Autenticação real (senha, bloqueio, MFA e federação disponíveis no Keycloak) sem código próprio.
  - Assinatura assimétrica: os serviços só precisam da chave pública (JWKS).
  - Caminho aberto para OIDC com redirecionamento e PKCE, e para outro provedor compatível (Entra ID).
- **Negativas:**
  - Mais um container na subida local e nos testes (alguns segundos a mais).
  - Pacotes do Aspire para Keycloak ainda em preview.
  - O password grant (ROPC) é desaconselhado pelo OAuth 2.1; fica restrito a Development e sai com #40 e #41.
  - O Keycloak 26 exige e-mail nos usuários para concluir o login.
