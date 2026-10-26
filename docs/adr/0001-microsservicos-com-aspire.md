# ADR 0001: Microsserviços orquestrados localmente com .NET Aspire

**Status:** aceita

## Contexto

O projeto quer mostrar uma arquitetura de microsserviços com mensageria, como os sistemas financeiros que o
mercado constrói hoje. Isso significa vários processos, quatro bancos, broker e cache. Sem uma ferramenta de
orquestração, rodar localmente exigiria docker-compose, portas fixas e connection strings espalhadas em cada serviço.

## Decisão

Usar o **.NET Aspire** como orquestrador de desenvolvimento:

- O **AppHost** declara a infraestrutura (Postgres com um banco por serviço, RabbitMQ e Redis), os serviços e o
  Gateway, com `WithReference`/`WaitFor`.
- As connection strings e os endereços são injetados pelo Aspire; o Gateway encontra os serviços por
  **service discovery** (`https+http://accounts-api`).
- O **ServiceDefaults** centraliza OpenTelemetry, health checks e resiliência de HTTP.
- Containers com `ContainerLifetime.Persistent`, para não recriar a infraestrutura a cada execução.

## Consequências

- **Positivas:**
  - Um comando sobe tudo.
  - O dashboard mostra logs, traces distribuídos (incluindo as mensagens do MassTransit) e métricas.
  - O modelo de recursos é testável (`AppHost.Tests`).
- **Negativas:**
  - O AppHost é uma ferramenta de desenvolvimento; o deploy precisa de um passo próprio (`azd`, Kubernetes).
  - Containers persistentes acumulam estado entre execuções.
