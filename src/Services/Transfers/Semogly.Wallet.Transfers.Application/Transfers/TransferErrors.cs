using Semogly.Wallet.SharedKernel.Results;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers;

public static class TransferErrors
{
    public static readonly Error InvalidIdempotencyKey = Error.Validation(
        "Transfer.InvalidIdempotencyKey",
        $"O header Idempotency-Key é obrigatório e deve ter até {Transfer.IdempotencyKeyMaxLength} caracteres.");

    public static readonly Error IdempotencyKeyReused = Error.Conflict(
        "Transfer.IdempotencyKeyReused",
        "A Idempotency-Key já foi usada em uma transferência com dados diferentes.");

    public static Error NotFound(Guid transferId) =>
        Error.NotFound("Transfer.NotFound", $"Transferência {transferId} não encontrada.");
}
