using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Application.Transfers.ListTransfers;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

/// <summary>Projeta direto para o DTO, sem rastreamento nem materialização do agregado.</summary>
internal sealed class TransferReadService(TransfersDbContext db) : ITransferReadService
{
    public Task<TransferResponse?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken) =>
        ToResponse(db.Transfers.AsNoTracking().Where(t => t.Id == transferId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<TransferResponse>> ListAsync(TransferFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Transfers.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(t => t.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.CreatedAt <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ToResponse(query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<TransferResponse>(items, filter.Page, filter.PageSize, total);
    }

    private static IQueryable<TransferResponse> ToResponse(IQueryable<Transfer> query) =>
        query.Select(t => new TransferResponse(
            t.Id,
            t.SourceAccountId,
            t.DestinationAccountId,
            t.Amount,
            t.Status,
            t.FailureReason,
            t.CreatedAt,
            t.UpdatedAt));
}
