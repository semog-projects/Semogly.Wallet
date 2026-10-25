using Semogly.Wallet.Ledger.Domain.Accounts;

namespace Semogly.Wallet.Ledger.Application.Accounts;

internal static class RepositoryExtensions
{
    /// <summary>Contas de cliente nascem do evento AccountCreated; contas de sistema, no primeiro uso.</summary>
    public static async Task<LedgerAccount> GetOrOpenSystemAccountAsync(
        this ILedgerAccountRepository accounts, Guid accountId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await accounts.GetAsync(accountId, cancellationToken)
        ?? LedgerAccount.Open(accountId, LedgerAccountType.System, now);

    /// <summary>Retorna a conta apenas se for de cliente: contas de sistema não recebem operações externas.</summary>
    public static async Task<LedgerAccount?> GetCustomerAccountAsync(
        this ILedgerAccountRepository accounts, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken);
        return account?.Type == LedgerAccountType.Customer ? account : null;
    }
}
