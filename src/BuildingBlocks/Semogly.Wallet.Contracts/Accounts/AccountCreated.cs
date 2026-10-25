namespace Semogly.Wallet.Contracts.Accounts;

/// <summary>Publicado pelo Accounts quando uma nova conta é aberta.</summary>
public sealed record AccountCreated(
    Guid AccountId,
    string OwnerName,
    string Email,
    string Currency,
    DateTimeOffset CreatedAt);
