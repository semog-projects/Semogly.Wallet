# ADR 0002: MassTransit 8 + RabbitMQ com Transactional Outbox/Inbox

**Status:** aceita

## Contexto

Os serviços se comunicam por eventos e comandos. Gravar no banco e publicar no broker são duas operações
independentes: se a aplicação cair entre elas, o dado fica sem evento (ou o evento sem dado). Além disso, o broker
entrega *at-least-once*, então a mesma mensagem pode chegar mais de uma vez.

## Decisão

- **RabbitMQ** como broker e **MassTransit 8.5** como abstração. A v9 do MassTransit exige licença comercial, por
  isso a versão está fixada na 8.
- **Transactional Outbox** (EF Core) em todos os serviços com banco relacional: a mensagem é gravada na mesma
  transação dos dados de negócio e entregue depois do commit.
- **Inbox**: deduplica as mensagens consumidas pelo `MessageId`, o que dá idempotência nos consumers.
- O retry (100ms → 5s) envolve o outbox, para que cada tentativa use uma transação nova. Esgotadas as tentativas,
  a mensagem vai para a fila `_error` (dead-letter).
- As filas são **prefixadas pelo serviço** (`fraud-…`, `transfers-…`). Assim, consumers com o mesmo nome em
  serviços diferentes têm filas próprias e cada serviço recebe sua cópia do evento.
- O Fraud, que não tem banco relacional, usa o **in-memory outbox**: só publica se o consumer concluir. A
  idempotência fica no próprio Redis (ver [ADR 0007](0007-antifraude-redis-lua.md)).

## Consequências

- **Positivas:**
  - Consistência entre dados e mensagens.
  - Mensagens duplicadas não geram efeitos duplicados.
  - Falhas transitórias se resolvem sozinhas.
- **Negativas:**
  - Tabelas extras (`InboxState`, `OutboxMessage`, `OutboxState`) em cada banco.
  - Latência adicional entre o commit e a entrega da mensagem.
