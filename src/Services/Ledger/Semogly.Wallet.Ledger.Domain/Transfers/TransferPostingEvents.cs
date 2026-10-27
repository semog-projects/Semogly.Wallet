namespace Semogly.Wallet.Ledger.Domain.Transfers;

// Eventos do stream da transferência (o valor movimentado fica nos streams das contas)

public sealed record TransferDebitRecorded(Guid TransferId, Guid SourceAccountId, decimal Amount, DateTimeOffset OccurredAt);

public sealed record TransferCreditRecorded(Guid TransferId, Guid DestinationAccountId, DateTimeOffset OccurredAt);

public sealed record TransferRefundRecorded(Guid TransferId, DateTimeOffset OccurredAt);

public sealed record TransferCancelled(Guid TransferId, DateTimeOffset OccurredAt);
