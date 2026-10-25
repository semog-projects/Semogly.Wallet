namespace Semogly.Wallet.Contracts.Transfers;

/// <summary>Publicado pelo Transfers ao aceitar uma transferência. Inicia a saga.</summary>
public sealed record TransferRequested(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    DateTimeOffset RequestedAt);

/// <summary>Publicado pela saga quando o débito e o crédito foram efetivados.</summary>
public sealed record TransferCompleted(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    DateTimeOffset CompletedAt);

/// <summary>Publicado pela saga quando a transferência é recusada ou estornada.</summary>
public sealed record TransferFailed(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Reason,
    DateTimeOffset FailedAt);
