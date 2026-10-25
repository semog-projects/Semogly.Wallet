using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers.CompleteTransfer;

/// <summary>Registra no agregado o sucesso informado pela saga.</summary>
public sealed record CompleteTransferCommand(Guid TransferId) : ICommand;

internal sealed class CompleteTransferCommandHandler(
    ITransferRepository transfers,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CompleteTransferCommand>
{
    public async Task<Result> HandleAsync(CompleteTransferCommand command, CancellationToken cancellationToken)
    {
        var transfer = await transfers.GetByIdAsync(command.TransferId, cancellationToken);

        if (transfer is null)
        {
            return Result.Failure(TransferErrors.NotFound(command.TransferId));
        }

        transfer.Complete(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
