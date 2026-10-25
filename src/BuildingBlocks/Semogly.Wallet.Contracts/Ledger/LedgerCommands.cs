namespace Semogly.Wallet.Contracts.Ledger;

// Comandos da saga de transferência para o Ledger. O resultado chega como
// AccountDebited/AccountCredited (sucesso) ou *Rejected (falha), correlacionados pelo TransferId.

public sealed record DebitAccount(Guid TransferId, Guid AccountId, decimal Amount);

public sealed record CreditAccount(Guid TransferId, Guid AccountId, decimal Amount);

public sealed record AccountDebitRejected(Guid TransferId, Guid AccountId, string Reason);

public sealed record AccountCreditRejected(Guid TransferId, Guid AccountId, string Reason);
