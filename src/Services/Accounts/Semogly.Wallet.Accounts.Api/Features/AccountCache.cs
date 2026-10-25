using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Api.Infrastructure;

namespace Semogly.Wallet.Accounts.Api.Features;

/// <summary>Leitura de contas com cache em dois níveis (memória + Redis) via HybridCache.</summary>
internal static class AccountCache
{
    public static string Key(Guid accountId) => $"accounts:{accountId}";

    public static ValueTask<AccountResponse?> GetAccountAsync(
        this HybridCache cache, AccountsDbContext db, Guid accountId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            Key(accountId),
            (db, accountId),
            // Lado de leitura: projeta direto para o DTO, sem materializar a entidade de domínio
            static async (state, token) => await state.db.Accounts
                .AsNoTracking()
                .Where(a => a.Id == state.accountId)
                .Select(a => new AccountResponse(a.Id, a.OwnerName, a.Email, a.Currency, a.Balance, a.Status, a.CreatedAt))
                .FirstOrDefaultAsync(token),
            cancellationToken: cancellationToken);
}
