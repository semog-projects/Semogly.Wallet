using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>Recibos como documentos do Marten, com Id = conta + chave.</summary>
internal sealed class MartenDepositReceiptStore(LedgerUnitOfWork unitOfWork) : IDepositReceiptStore
{
    public async Task<DepositReceipt?> FindAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var session = await unitOfWork.GetSessionAsync(cancellationToken);
        var stored = await session.LoadAsync<StoredDepositReceipt>(StoredDepositReceipt.IdFor(accountId, idempotencyKey), cancellationToken);

        return stored?.Receipt;
    }

    public async Task AddAsync(DepositReceipt receipt, CancellationToken cancellationToken)
    {
        var session = await unitOfWork.GetSessionAsync(cancellationToken);

        // Insert (e não Store): um recibo já gravado por um pedido simultâneo derruba a transação inteira
        session.Insert(new StoredDepositReceipt(StoredDepositReceipt.IdFor(receipt.AccountId, receipt.IdempotencyKey), receipt));
    }
}

public sealed record StoredDepositReceipt(string Id, DepositReceipt Receipt)
{
    public static string IdFor(Guid accountId, string idempotencyKey) => $"{accountId:N}:{idempotencyKey}";
}
