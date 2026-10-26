# ADR 0007: Regras antifraude com estado no Redis via script Lua atômico

**Status:** aceita

## Contexto

O limite diário e o velocity check dependem do histórico recente da conta e precisam ser rápidos. Com a
abordagem "ler os contadores, decidir e depois incrementar", duas análises simultâneas da mesma conta leriam o
mesmo valor e ambas aprovariam, furando o limite.

## Decisão

- **Regras com estado no Redis:**
  - `fraud:daily:{conta}:{data}`: hash `TransferId → centavos`, com a data no fuso de Brasília.
  - `fraud:velocity:{conta}`: sorted set por timestamp, para a janela deslizante.
- **Script Lua único:** confere o limite diário e o velocity e só então registra a transferência. O Redis executa
  scripts de forma atômica.
- **Registro idempotente:** indexado pelo `TransferId`, então a reentrega da mensagem não conta duas vezes.
- **Liberação do limite:** `TransferFailed` (por exemplo, saldo insuficiente) devolve o valor ao limite diário.
- **Regra sem estado no domínio:** o limite por transação é avaliado antes, e uma recusa por ele não consome
  limite diário nem velocity.
- **Valores em centavos inteiros**, para evitar erro de ponto flutuante no Lua.

## Consequências

- **Positivas:**
  - Decisão em uma única ida ao Redis.
  - Correta sob concorrência (comprovado por teste de integração com 20 análises simultâneas).
- **Negativas:**
  - Lógica de negócio dentro de um script Lua, mais difícil de testar sem um Redis real.
  - Os contadores são voláteis: dependem da persistência configurada no Redis.
