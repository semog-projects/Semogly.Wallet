# ADR 0005: Ledger com Event Sourcing (Marten) e partidas dobradas

**Status:** aceita

## Contexto

Em uma carteira digital, o saldo precisa ser explicável centavo a centavo, auditável e reconstruível em qualquer
ponto do tempo. Guardar só o saldo atual, sobrescrevendo-o, perde esse histórico.

## Decisão

- **Event Sourcing com Marten:** cada conta é um stream (`LedgerAccountOpened`, `FundsDebited`, `FundsCredited`).
  Os lançamentos são imutáveis e correções viram novos lançamentos (estornos).
- **Domínio puro:** o agregado `LedgerAccount` não conhece o Marten. O repositório reconstrói o agregado a partir
  dos eventos e grava os pendentes com concorrência otimista pela versão do stream.
- **Partidas dobradas:** `DoubleEntry.Post` sempre gera débito e crédito de mesmo valor em contas diferentes.
  - **CashIn:** contrapartida dos depósitos.
  - **Clearing:** valores em trânsito durante a saga.
  - A soma de todos os saldos do livro é sempre zero.
- **Snapshots:** a cada 25 eventos, o repositório grava o estado da conta (`LedgerAccountSnapshot`, documento
  do Marten) na mesma transação dos eventos. A carga lê o snapshot e só os eventos posteriores
  (`LedgerAccount.Restore`). Contas sem snapshot, como as anteriores a ele, são reconstruídas pelo stream inteiro
  até cruzarem o próximo múltiplo. Snapshot manual, e não uma projeção do Marten: dispensa o rebuild das contas existentes e
  o agregado continua sem convenções do Marten.
- **Depósito idempotente:** `Idempotency-Key` obrigatória, por conta. O recibo (documento com Id = conta + chave)
  é inserido na mesma transação do lançamento:
  - Repetir com a mesma chave devolve o lançamento original.
  - A mesma chave com outro valor dá 409.
  - Num pedido simultâneo com a mesma chave, o segundo `Insert` falha no commit e responde 409, sem lançar duas vezes.
- **Extrato:** projeção inline (`StatementProjection`) com o modo de append `Rich`, que disponibiliza a
  sequência do evento para ordenar o extrato.
- **Saldo no Accounts:** o Accounts mantém só uma **projeção** do saldo, atualizada pelos eventos
  `AccountDebited`/`AccountCredited`. A decisão (por exemplo, saldo insuficiente) é sempre do Ledger.

## Consequências

- **Positivas:**
  - Histórico completo e auditável.
  - O invariante contábil é verificável (soma zero).
  - O extrato sai de graça a partir dos eventos.
- **Negativas:**
  - Os snapshots são um documento a mais por conta e precisam acompanhar mudanças no estado do agregado
    (um campo novo exige descartar os snapshots antigos).
  - A conta Clearing era um ponto de contenção (stream "quente") sob alta concorrência; hoje é particionada
    ([ADR 0013](0013-capacidade-sob-carga.md)). A CashIn segue única (os depósitos são bem menos frequentes).
