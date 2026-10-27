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
- **Login do web (BFF):** o Gateway é o cliente OIDC confidencial (`wallet-gateway`):
  - `GET /bff/login` faz Authorization Code + PKCE com PAR (os parâmetros vão ao Keycloak pelo canal de
    servidor) e `response_mode=query`; o retorno (`/bff/signin-oidc`) cria a sessão e volta a um caminho local.
  - A sessão guarda access token, refresh token e expiração; perto de expirar, o Gateway renova os tokens
    (`OnValidatePrincipal`). Se o Keycloak recusar a renovação, a sessão do BFF é encerrada.
  - `POST /bff/logout` encerra a sessão no Keycloak pelo canal de servidor (endpoint de logout com o refresh
    token), sem depender de redirecionar o navegador.
  - O front informa sua origem pública por `X-Forwarded-Host/Proto` (aceitos só de proxies locais).
- **Password grant só em Development** (`POST /auth/token`), para o Scalar, o curl e o app até a issue #41.
- **Sessão do BFF no servidor:** os tokens RS256 do Keycloak deixariam o cookie grande demais (fatiado em vários
  `Set-Cookie`). O ticket fica no Redis, protegido pelo Data Protection, e o cookie carrega só a chave da sessão.
- **Testes de integração:** `Testcontainers.Keycloak` importa o mesmo arquivo de realm; usuários são criados pela
  API de administração e os testes usam tokens reais. O login do BFF é testado de ponta a ponta, preenchendo a
  tela de login do Keycloak.
- **HSTS desligado no realm de desenvolvimento** (`max-age=0`): o HSTS vale para o host, não para a porta, e o
  enviado por `https://localhost:8443` forçaria https no front em `http://localhost:5173`.

## Consequências

- **Positivas:**
  - Autenticação real (senha, bloqueio, MFA e federação disponíveis no Keycloak) sem código próprio.
  - Assinatura assimétrica: os serviços só precisam da chave pública (JWKS).
  - Caminho aberto para OIDC com redirecionamento e PKCE, e para outro provedor compatível (Entra ID).
- **Negativas:**
  - Mais um container na subida local e nos testes (alguns segundos a mais).
  - Pacotes do Aspire para Keycloak ainda em preview.
  - O password grant (ROPC) é desaconselhado pelo OAuth 2.1; fica restrito a Development e sai do app com a #41.
  - O realm versionado é de desenvolvimento (segredo do cliente, HTTP permitido, HSTS desligado); produção
    precisa de outra configuração.
  - O Keycloak 26 exige e-mail nos usuários para concluir o login.
