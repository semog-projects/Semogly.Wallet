namespace Semogly.Wallet.Contracts.Fraud;

/// <summary>Comando da saga de transferência para o Fraud avaliar o risco da operação.</summary>
public sealed record CheckTransferFraud(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount);

public sealed record TransferFraudCheckApproved(Guid TransferId);

public sealed record TransferFraudCheckRejected(Guid TransferId, string Reason);
