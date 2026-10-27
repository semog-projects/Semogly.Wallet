namespace Semogly.Wallet.Ledger.Domain.Transfers;

/// <summary>Repositório event-sourced dos lançamentos por transferência.</summary>
public interface ITransferPostingRepository
{
    /// <summary>Lançamentos da transferência; sem stream ainda, uma transferência sem lançamentos.</summary>
    Task<TransferPosting> GetAsync(Guid transferId, CancellationToken cancellationToken);

    Task SaveAsync(TransferPosting posting, CancellationToken cancellationToken);
}
