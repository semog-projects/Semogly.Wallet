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
  - A reconstrução lê o stream inteiro; contas com muitos lançamentos vão precisar de snapshots.
  - A conta Clearing é um ponto de contenção (stream "quente") sob alta concorrência.
