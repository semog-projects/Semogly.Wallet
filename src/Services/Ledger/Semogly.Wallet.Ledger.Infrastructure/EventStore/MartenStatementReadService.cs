using Marten;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

internal sealed class MartenStatementReadService(IQuerySession session) : IStatementReadService
{
    public async Task<StatementResponse?> GetAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (await session.Events.FetchStreamStateAsync(accountId, cancellationToken) is null)
        {
            return null;
        }

        var entries = await AccountEntries(accountId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // O saldo atual é o saldo após o lançamento mais recente
        var latest = page == 1
            ? (entries.Count > 0 ? entries[0] : null)
            : await AccountEntries(accountId).FirstOrDefaultAsync(cancellationToken);

        return new StatementResponse(
            accountId,
            latest?.BalanceAfter ?? 0m,
            page,
            pageSize,
            entries
                .Select(e => new StatementEntryResponse(
                    e.Id, e.Type, e.Amount, e.BalanceAfter, e.TransferId, e.Description, e.OccurredAt))
                .ToList());
    }

    private IQueryable<StatementEntry> AccountEntries(Guid accountId) =>
        session.Query<StatementEntry>()
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.Sequence);
}
