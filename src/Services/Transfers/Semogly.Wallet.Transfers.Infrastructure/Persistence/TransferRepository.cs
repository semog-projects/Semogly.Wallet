using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

internal sealed class TransferRepository(TransfersDbContext db) : ITransferRepository
{
    public Task<Transfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Transfers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        db.Transfers.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

    public void Add(Transfer transfer) => db.Transfers.Add(transfer);
}
