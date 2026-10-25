using Semogly.Wallet.Accounts.Api.Domain;

namespace Semogly.Wallet.Accounts.Api.Features;

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
