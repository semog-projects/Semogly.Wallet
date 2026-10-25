using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;

/// <summary>
/// Registra a transferência e publica <see cref="TransferRequested"/> (via outbox) para iniciar a saga.
/// Pedidos repetidos com a mesma Idempotency-Key devolvem a transferência original.
/// </summary>
internal sealed class CreateTransferCommandHandler(
    ITransferRepository transfers,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreateTransferCommand, TransferResponse>
{
    public async Task<Result<TransferResponse>> HandleAsync(CreateTransferCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) ||
            command.IdempotencyKey.Length > Transfer.IdempotencyKeyMaxLength)
        {
            return TransferErrors.InvalidIdempotencyKey;
        }

        var existing = await transfers.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return Replay(existing, command);
        }

        var transfer = Transfer.Request(
            command.SourceAccountId,
            command.DestinationAccountId,
            command.Amount,
            command.IdempotencyKey,
            clock.GetUtcNow());

        transfers.Add(transfer);

        await publisher.PublishAsync(
            new TransferRequested(
                transfer.Id,
                transfer.SourceAccountId,
                transfer.DestinationAccountId,
                transfer.Amount,
                transfer.CreatedAt),
            cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Requisição concorrente com a mesma chave gravou primeiro: devolve a que venceu
            existing = await transfers.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);

            if (existing is null)
            {
                throw;
            }

            return Replay(existing, command);
        }

        return TransferResponse.From(transfer);
    }

    private static Result<TransferResponse> Replay(Transfer existing, CreateTransferCommand command) =>
        existing.IsSameRequest(command.SourceAccountId, command.DestinationAccountId, command.Amount)
            ? TransferResponse.From(existing)
            : TransferErrors.IdempotencyKeyReused;
}
