# ADR 0013: Ajustes de capacidade a partir do teste de carga

**Status:** aceita

## Contexto

O teste de carga com k6 (`tests/load`) rodou contra o ambiente local completo. A 3 transferências/s tudo passava,
mas a 8/s apareceram três problemas, nessa ordem de investigação:

1. **Mensagens na fila de erro** do Ledger: `40001 could not serialize access` no débito e, no crédito, um
   `TypeLoadException` do MassTransit ao publicar o fault da mesma falha. Toda transferência lança no stream da conta
   **Clearing**: com vários consumers em paralelo, quase todos conflitam. Como o retry é determinístico (sem jitter),
   os perdedores repetem juntos e voltam a colidir até esgotar as tentativas.
2. **Vazão travada em ~4/s com a CPU ociosa** e `POST /transfers` com p95 de 9 s: o Postgres recusava conexões
   (`sorry, too many clients already`). O padrão é 100 conexões, e cada serviço tem os próprios pools (EF, Marten,
   Quartz), que crescem sob carga.
3. Um bug do próprio script (`0.01 * 35 = 0.35000000000000003`, recusado pela regra de duas casas decimais), que
   o teste expôs como 422.

## Decisão

- **Conta Clearing particionada** em 8 contas (`SystemAccounts.ClearingFor(transferId)`): débito, crédito e estorno
  de uma transferência usam sempre a mesma partição, que volta a zero quando ela termina. O livro soma as partições
  (e a conta única antiga, com saldo zero). É a solução usual para contas "quentes" em livros contábeis: a contenção
  cai na proporção do número de partições.
- **Retry dedicado a conflitos de concorrência** no building block de mensageria, antes do retry geral: 10
  tentativas rápidas (10 ms a 500 ms) para concorrência otimista do EF, falhas transitórias do Postgres
  (serialização, deadlock) e versão de stream inesperada do Marten. O retry geral (4 tentativas espaçadas) continua
  para as demais falhas.
- **Concorrência limitada** nos consumers de lançamento do Ledger (4 por fila): mais paralelismo sobre os mesmos
  streams só gera conflitos.
- **`max_connections=500`** no Postgres do AppHost. Em produção, o caminho é um pooler (PgBouncer) e pools
  dimensionados por serviço.
- **Depósitos:** um 409 por conflito na conta CashIn é repetido pelo cliente com a mesma `Idempotency-Key`, que
  garante um único lançamento.

## Consequências

- **Positivas:**
  - A 8 transferências/s, todas as 960 transferências concluem, com zero falhas e zero mensagens em fila de erro;
    `POST /transfers` p95 de 32 ms (era 9 s) e transferência ponta a ponta com p95 de 1,4 s.
  - Os invariantes (livro balanceado, Clearing zerado, Accounts igual ao Ledger) são verificados a cada execução.
- **Negativas:**
  - O número de partições é fixo no código; mudá-lo exige que não haja transferências em andamento.
  - O teste de carga não roda no CI: precisa do ambiente completo do AppHost.
  - Os números são de uma única máquina rodando tudo (serviços em Debug, containers e o próprio k6), servem para
    comparar versões, não como capacidade de produção.
