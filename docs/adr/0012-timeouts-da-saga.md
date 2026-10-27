# ADR 0012: Timeouts da saga com cancelamento arbitrado pelo Ledger

**Status:** aceita (complementa a [ADR 0003](0003-saga-orquestrada.md))

## Contexto

A saga esperava indefinidamente por cada resposta: com o antifraude ou o Ledger fora do ar, a transferência ficava
`Pending` para sempre. Um timeout resolve a espera, mas a reação ingênua ao timeout do crédito, que é estornar a
origem, é perigosa: o crédito pode estar só atrasado na fila. Se ele for lançado depois, o destino recebe e a origem
também, e o dinheiro duplica. O mesmo vale para um débito atrasado depois de a transferência ser dada como falha.

## Decisão

- **Prazos** em `TransferSaga` (configuração do Transfers): `FraudCheckTimeout` (30 s) e `LedgerTimeout` (1 min, por
  lançamento). A saga agenda a mensagem de prazo ao pedir cada passo e cancela o agendamento quando a resposta chega.
- **Antifraude sem resposta:** a transferência falha ("O antifraude não respondeu a tempo."). É seguro porque nenhum
  lançamento foi pedido; uma aprovação tardia é descartada (a saga já terminou).
- **Débito ou crédito sem resposta:** a saga não decide sozinha. Ela publica `CancelTransferPosting` e espera em
  `AwaitingCancellation`, ignorando lançamentos e recusas atrasados.
- **O Ledger é o árbitro**, porque só ele sabe o que já foi lançado. Cada transferência tem um stream próprio no Marten
  (`TransferPosting`: débito, crédito, estorno e cancelamento), gravado na mesma transação dos streams das contas:
  - Crédito já lançado → `TransferPostingAlreadyCompleted`; a saga conclui a transferência.
  - Caso contrário → cancela; se o débito já foi lançado, estorna a origem. Responde `TransferPostingCancelled`
    (`Refunded` indica o estorno) e a saga falha com "O livro-razão não respondeu a tempo." (com o prefixo
    "Estornada: " quando houve estorno).
  - Depois do cancelamento, débito e crédito atrasados não são lançados. Débitos e créditos repetidos (reentrega)
    também não, já que o stream registra o que foi lançado.
  - A concorrência otimista do stream impede que cancelamento e crédito simultâneos sejam aceitos juntos.
- **Agendador:** Quartz.NET pelo agendador do MassTransit (`AddPublishMessageScheduler` + `AddQuartzConsumers`), com
  armazenamento no Postgres do Transfers (tabelas criadas por migration do EF) e em cluster:
  - O agendamento sobrevive a reinícios; prazos vencidos durante uma parada disparam na volta.
  - Com várias instâncias, cada agendamento dispara uma única vez.

## Alternativas consideradas

- **Plugin de mensagens atrasadas do RabbitMQ:** exige uma imagem customizada do broker, tanto no AppHost quanto nos
  testes, e o plugin tem limitações conhecidas de durabilidade e escala.
- **Varredura periódica das sagas vencidas** (serviço em segundo plano): sem dependência nova, mas reinventa o
  agendamento e fica fora do modelo de `Schedule` do state machine.
- **Estornar direto no timeout do crédito:** descartado pelo risco de dinheiro duplicado descrito acima.

## Consequências

- **Positivas:**
  - Nenhuma transferência fica pendente para sempre, e nenhum timeout move dinheiro por suposição.
  - O Ledger passa a ser idempotente por transferência, não só por `MessageId` (Inbox).
- **Negativas:**
  - Mais um componente (Quartz) e onze tabelas no banco do Transfers.
  - Mais um stream por transferência no event store do Ledger.
  - Se o Ledger estiver fora do ar, a transferência fica em `AwaitingCancellation` até ele voltar: o pedido de
    cancelamento é durável e será processado, mas o desfecho espera por ele.
