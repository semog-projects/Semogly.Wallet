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
- **JWT Bearer** em todas as rotas `/api`; o depósito exige o papel `operator`. Em Development, o Gateway emite
  tokens (`POST /auth/token`) assinados com uma chave gerada pelo Aspire como parâmetro secreto.
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
- **Negativas:**
  - O emissor de tokens é só para desenvolvimento; produção precisa de um Identity Provider.
  - Os serviços confiam no Gateway (ainda não validam o JWT por conta própria).
  - A posse da conta (o usuário só acessar as próprias contas) ainda não é verificada.
