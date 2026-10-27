using Semogly.Wallet.Ledger.Application.Accounts.Deposit;

namespace Semogly.Wallet.Ledger.Application.Abstractions;

/// <summary>
/// Recibos de depósito por Idempotency-Key (por conta). Gravados na mesma transação do lançamento: dois pedidos
/// simultâneos com a mesma chave não lançam duas vezes, porque o segundo recibo é recusado no commit.
/// </summary>
public interface IDepositReceiptStore
{
    Task<DepositReceipt?> FindAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken);

    Task AddAsync(DepositReceipt receipt, CancellationToken cancellationToken);
}
