using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Application.Abstractions;

namespace Semogly.Wallet.Accounts.Infrastructure.Caching;

internal sealed class HybridAccountCache(HybridCache cache) : IAccountCache
{
    public async Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken) =>
        await cache.RemoveAsync(AccountCacheKeys.Account(accountId), cancellationToken);
}
