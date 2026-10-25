namespace Semogly.Wallet.Ledger.Domain.Accounts;

// Eventos de domínio gravados no event store. São imutáveis: correções viram novos lançamentos.

public sealed record LedgerAccountOpened(Guid AccountId, LedgerAccountType Type, DateTimeOffset OpenedAt);

public sealed record FundsDebited(
    Guid EntryId,
    Guid AccountId,
    decimal Amount,
    decimal BalanceAfter,
    Guid? TransferId,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record FundsCredited(
    Guid EntryId,
    Guid AccountId,
    decimal Amount,
    decimal BalanceAfter,
    Guid? TransferId,
    string Description,
    DateTimeOffset OccurredAt);
