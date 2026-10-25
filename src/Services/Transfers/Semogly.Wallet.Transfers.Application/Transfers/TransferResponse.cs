using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers;

public sealed record TransferResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    TransferStatus Status,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static TransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.SourceAccountId,
        transfer.DestinationAccountId,
        transfer.Amount,
        transfer.Status,
        transfer.FailureReason,
        transfer.CreatedAt,
        transfer.UpdatedAt);
}
