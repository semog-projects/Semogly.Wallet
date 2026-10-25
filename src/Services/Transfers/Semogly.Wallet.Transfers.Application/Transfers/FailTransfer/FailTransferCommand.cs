using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers.FailTransfer;

/// <summary>Registra no agregado a falha (ou o estorno) informada pela saga.</summary>
public sealed record FailTransferCommand(Guid TransferId, string Reason) : ICommand;

internal sealed class FailTransferCommandHandler(
    ITransferRepository transfers,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<FailTransferCommand>
{
    public async Task<Result> HandleAsync(FailTransferCommand command, CancellationToken cancellationToken)
    {
        var transfer = await transfers.GetByIdAsync(command.TransferId, cancellationToken);

        if (transfer is null)
        {
            return Result.Failure(TransferErrors.NotFound(command.TransferId));
        }

        transfer.Fail(command.Reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
