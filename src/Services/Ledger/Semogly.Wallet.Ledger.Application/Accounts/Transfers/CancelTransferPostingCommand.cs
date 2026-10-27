using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Transfers;

/// <summary>
/// Timeout da saga: decide o desfecho de uma transferência sem resposta. Se o crédito já foi lançado, a transferência
/// se completou; caso contrário, ela é cancelada e um débito já lançado é estornado.
/// </summary>
public sealed record CancelTransferPostingCommand(Guid TransferId) : ICommand;

internal sealed class CancelTransferPostingCommandHandler(
    ILedgerAccountRepository accounts,
    ITransferPostingRepository postings,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelTransferPostingCommand>
{
    public async Task<Result> HandleAsync(CancelTransferPostingCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var posting = await postings.GetAsync(command.TransferId, cancellationToken);

        if (posting.IsCredited)
        {
            await publisher.PublishAsync(new TransferPostingAlreadyCompleted(command.TransferId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        posting.Cancel(now);

        if (posting.NeedsRefund)
        {
            await TransferRefund.PostAsync(accounts, postings, publisher, posting, now, cancellationToken);
        }
        else
        {
            await postings.SaveAsync(posting, cancellationToken);
        }

        await publisher.PublishAsync(new TransferPostingCancelled(command.TransferId, posting.IsRefunded), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
