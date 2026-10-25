using Semogly.Wallet.Accounts.Domain.Accounts;

namespace Semogly.Wallet.Accounts.Application.Accounts;

public sealed record AccountResponse(
    Guid Id,
    string OwnerName,
    string Email,
    string Currency,
    decimal Balance,
    AccountStatus Status,
    DateTimeOffset CreatedAt)
{
    public static AccountResponse From(Account account) => new(
        account.Id,
        account.OwnerName,
        account.Email,
        account.Currency,
        account.Balance,
        account.Status,
        account.CreatedAt);
}
