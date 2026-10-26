# ADR 0003: Saga orquestrada para transferências

**Status:** aceita

## Contexto

Uma transferência envolve três serviços (Fraud, Ledger e Transfers) e não pode usar uma transação distribuída.
Se o crédito no destino falhar depois de a origem já ter sido debitada, o sistema precisa compensar.

## Decisão

Usar uma **saga orquestrada** com o state machine do MassTransit (`TransferStateMachine`), em vez de coreografia:

- **Estados:** `AwaitingFraudCheck → AwaitingDebit → AwaitingCredit → (AwaitingRefund)`.
- **Compensação:** se o crédito for recusado, a saga envia um `CreditAccount` de volta para a origem (estorno) e
  encerra como `Failed` com o motivo "Estornada: …".
- **Persistência:** o estado da saga fica no Postgres do Transfers, com **concorrência otimista** (`xmin`), no
  mesmo `DbContext` do outbox. A transição de estado e as mensagens publicadas são atômicas.
- **Separação de papéis:** o agregado `Transfer` (Domain) registra o pedido e o desfecho. A saga (Infrastructure)
  só orquestra e publica `TransferCompleted`/`TransferFailed`, que o próprio Transfers e o Notifications consomem.
- **Mensagens tardias:** mensagens de sagas já finalizadas são descartadas (`OnMissingInstance → Discard`).

## Consequências

- **Positivas:**
  - O fluxo inteiro fica explícito em um único lugar e é fácil de testar com o test harness.
  - A compensação é determinística.
- **Negativas:**
  - O Transfers concentra o conhecimento do fluxo.
  - Ainda não há timeout para respostas que não chegam, como o antifraude indisponível.
