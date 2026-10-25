namespace Semogly.Wallet.Contracts.Ledger;

/// <summary>Publicado pelo Ledger quando um lançamento de débito é efetivado em uma conta.</summary>
public sealed record AccountDebited(
    Guid EntryId,
    Guid AccountId,
    decimal Amount,
    Guid? TransferId,
    DateTimeOffset OccurredAt);
