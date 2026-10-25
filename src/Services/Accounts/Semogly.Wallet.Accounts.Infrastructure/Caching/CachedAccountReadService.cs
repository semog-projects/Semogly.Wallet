using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Infrastructure.Persistence;

namespace Semogly.Wallet.Accounts.Infrastructure.Caching;

/// <summary>Lado de leitura com cache em dois níveis (memória + Redis) via HybridCache.</summary>
internal sealed class CachedAccountReadService(HybridCache cache, AccountsDbContext db) : IAccountReadService
{
    public async Task<AccountResponse?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            AccountCacheKeys.Account(accountId),
            (db, accountId),
            // Projeta direto para o DTO, sem materializar a entidade de domínio
            static async (state, token) => await state.db.Accounts
                .AsNoTracking()
                .Where(a => a.Id == state.accountId)
                .Select(a => new AccountResponse(a.Id, a.OwnerName, a.Email, a.Currency, a.Balance, a.Status, a.CreatedAt))
                .FirstOrDefaultAsync(token),
            cancellationToken: cancellationToken);
}
