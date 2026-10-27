namespace Semogly.Wallet.Contracts.Ledger;

// Comandos da saga de transferência para o Ledger. O resultado chega como
// AccountDebited/AccountCredited (sucesso) ou *Rejected (falha), correlacionados pelo TransferId.

public sealed record DebitAccount(Guid TransferId, Guid AccountId, decimal Amount);

public sealed record CreditAccount(Guid TransferId, Guid AccountId, decimal Amount);

public sealed record AccountDebitRejected(Guid TransferId, Guid AccountId, string Reason);

public sealed record AccountCreditRejected(Guid TransferId, Guid AccountId, string Reason);

// Timeout da saga: o Ledger decide o desfecho de uma transferência sem resposta. Depois do cancelamento, débito e
// crédito atrasados não são lançados; se o débito já foi lançado (e o crédito não), ele é estornado.

public sealed record CancelTransferPosting(Guid TransferId);

/// <summary>Transferência cancelada no Ledger; Refunded indica que um débito já lançado foi devolvido à origem.</summary>
public sealed record TransferPostingCancelled(Guid TransferId, bool Refunded);

/// <summary>Cancelamento recusado: o crédito no destino já foi lançado, então a transferência se completou.</summary>
public sealed record TransferPostingAlreadyCompleted(Guid TransferId);
