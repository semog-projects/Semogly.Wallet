namespace Semogly.Wallet.Accounts.Infrastructure.Caching;

internal static class AccountCacheKeys
{
    public static string Account(Guid accountId) => $"accounts:{accountId}";
}
