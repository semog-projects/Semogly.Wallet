# ADR 0008: API Gateway com YARP, JWT e rate limiting

**Status:** aceita

## Contexto

Com cinco serviços, expor cada um diretamente espalharia autenticação, limites de uso e documentação, além de
obrigar o cliente a conhecer cada endereço.

## Decisão

- **YARP** como reverse proxy:
  - Rotas `/api/{serviço}/...` definidas em configuração.
  - Destinos resolvidos pelo service discovery do Aspire.
  - Só o Gateway tem endpoint externo.
- **JWT Bearer** em todas as rotas `/api`; o depósito exige o papel `operator`. Os tokens vêm do Keycloak
  ([ADR 0010](0010-keycloak-identity-provider.md)); no início, o próprio Gateway emitia tokens HS256 de
  desenvolvimento.
- **Rate limiting** por usuário (claim `sub`, ou IP sem autenticação):
  - Geral: 100 requisições por minuto.
  - `POST /transfers`: token bucket (rajada de 10, ~10 por minuto).
  - Resposta 429 com `Retry-After`.
- **Scalar agregado:**
  - O Gateway repassa o OpenAPI de cada serviço com o header `X-Gateway-Prefix`.
  - Um transformer no ServiceDefaults aponta o servidor do documento para `/api` e declara o esquema Bearer, para
    que o "Try it" também passe pelo Gateway.

## Consequências

- **Positivas:** segurança, limites e documentação concentrados em um ponto, e os serviços ficam focados no negócio.
- **Negativas** (resolvidas pela [ADR 0011](0011-posse-da-conta.md)):
  - Só o Accounts validava o JWT por conta própria; os demais serviços confiavam no Gateway.
  - A posse da conta (o usuário só acessar as próprias contas) não era verificada.
