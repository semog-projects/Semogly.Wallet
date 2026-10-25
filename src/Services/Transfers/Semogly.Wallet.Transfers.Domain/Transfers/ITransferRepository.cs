namespace Semogly.Wallet.Transfers.Domain.Transfers;

/// <summary>Repositório do agregado <see cref="Transfer"/>, usado pelo lado de escrita.</summary>
public interface ITransferRepository
{
    Task<Transfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    void Add(Transfer transfer);
}
