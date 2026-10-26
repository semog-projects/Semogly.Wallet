# ADR 0009: Testes de integração com Testcontainers e serviços em memória

**Status:** aceita

## Contexto

Os pontos mais delicados do sistema dependem de infraestrutura real: a transação compartilhada Marten + EF, o
script Lua no Redis, a saga persistida no Postgres e o fluxo assíncrono entre os serviços pelo RabbitMQ. Fakes
não reproduzem esses comportamentos.

## Decisão

- **Testcontainers** sobe Postgres, RabbitMQ e Redis com as mesmas imagens usadas pelo AppHost.
- Os **cinco serviços reais** são hospedados em memória via `WebApplicationFactory`, conectados aos containers
  com as mesmas connection strings que o Aspire injeta. Os `Program` de nível superior têm o mesmo nome global, por
  isso cada API expõe um tipo marcador (`XxxApiMarker`) para o factory.
- Um **fixture compartilhado** inicializa tudo uma vez por execução.
- O helper `Eventually` aguarda a consistência eventual em vez de usar esperas fixas.
- A pirâmide de testes fica assim:
  - **Unitários:** domínio, handlers e a saga no test harness do MassTransit.
  - **Arquitetura:** regra de dependência da Clean Architecture.
  - **Modelo do AppHost:** recursos e referências do Aspire.
  - **Integração:** infraestrutura real.

## Consequências

- **Positivas:**
  - Os cenários críticos são testados de ponta a ponta: sucesso, compensação, saldo insuficiente, antifraude,
    idempotência concorrente, atomicidade e concorrência no Redis.
  - Rodam no CI (o runner do GitHub Actions tem Docker).
- **Negativas:**
  - Exige Docker.
  - A execução é mais lenta (~30–40s, incluindo a subida dos containers).
  - O Gateway fica fora desses testes; ele é validado manualmente e pelo teste de modelo do AppHost.
