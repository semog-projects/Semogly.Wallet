namespace Semogly.Wallet.Contracts.Ledger;

/// <summary>Publicado pelo Ledger quando um lançamento de crédito é efetivado em uma conta.</summary>
public sealed record AccountCredited(
    Guid EntryId,
    Guid AccountId,
    decimal Amount,
    Guid? TransferId,
    DateTimeOffset OccurredAt);
