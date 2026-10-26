# ADR 0006: Transação compartilhada entre Marten e o outbox do EF Core

**Status:** aceita

## Contexto

O Ledger grava eventos no Marten, mas o outbox/inbox do MassTransit é implementado sobre EF Core. Com duas
transações separadas, um lançamento poderia ser gravado sem publicar o `AccountDebited` (ou o contrário), e uma
mensagem duplicada poderia ser lançada duas vezes.

## Decisão

Fazer o Marten **participar da transação do EF Core** na mesma conexão do Postgres (`LedgerUnitOfWork`):

- **Em consumers:** o outbox do MassTransit abre a transação no `LedgerDbContext`. A sessão do Marten entra nela via
  `SessionOptions.ForTransaction(...)`.
- **Em requisições HTTP** (depósito): o próprio unit of work abre a transação, grava os eventos do Marten (que não
  faz commit, pois a transação é externa), grava o outbox e então confirma.
- O retry automático do EF (`DisableRetry`) fica desligado, porque ele não suporta transações iniciadas pelo
  usuário. O retry acontece no nível da mensagem, pelo MassTransit.

## Consequências

- **Positivas:** eventos do livro-razão, mensagens publicadas e deduplicação do inbox são confirmados ou
  descartados juntos. Um teste de integração comprova que os eventos só ficam visíveis após o commit.
- **Negativas:** acoplamento ao Postgres (as duas bibliotecas precisam estar no mesmo banco) e um unit of work
  mais elaborado que nos outros serviços.
