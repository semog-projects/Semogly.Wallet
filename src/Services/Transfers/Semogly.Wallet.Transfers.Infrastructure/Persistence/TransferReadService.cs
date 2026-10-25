using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Transfers.Application.Transfers;

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

/// <summary>Projeta direto para o DTO, sem rastreamento nem materialização do agregado.</summary>
internal sealed class TransferReadService(TransfersDbContext db) : ITransferReadService
{
    public Task<TransferResponse?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken) =>
        db.Transfers
            .AsNoTracking()
            .Where(t => t.Id == transferId)
            .Select(t => new TransferResponse(
                t.Id,
                t.SourceAccountId,
                t.DestinationAccountId,
                t.Amount,
                t.Status,
                t.FailureReason,
                t.CreatedAt,
                t.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
