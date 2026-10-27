using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Transfers;

/// <summary>
/// Credita o destino (ou estorna a origem) de uma transferência, retirando o valor da conta de trânsito (Clearing).
/// </summary>
public sealed record CreditForTransferCommand(Guid TransferId, Guid AccountId, decimal Amount) : ICommand;

internal sealed class CreditForTransferCommandHandler(
    ILedgerAccountRepository accounts,
    ITransferPostingRepository postings,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreditForTransferCommand>
{
    public const string Description = "Recebimento de transferência";

    public async Task<Result> HandleAsync(CreditForTransferCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var posting = await postings.GetAsync(command.TransferId, cancellationToken);

        // Compensação da saga (crédito recusado no destino): devolve o débito à origem
        if (posting.IsRefundTo(command.AccountId))
        {
            if (posting.NeedsRefund)
            {
                await TransferRefund.PostAsync(accounts, postings, publisher, posting, now, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Cancelada por timeout da saga ou crédito já lançado: nada a fazer
        if (!posting.CanCredit)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var destination = await accounts.GetCustomerAccountAsync(command.AccountId, cancellationToken);

        if (destination is null)
        {
            await publisher.PublishAsync(
                new AccountCreditRejected(command.TransferId, command.AccountId, LedgerErrors.AccountNotFoundReason),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var clearing = await accounts.GetOrOpenSystemAccountAsync(SystemAccounts.Clearing, now, cancellationToken);
        var (_, credit) = DoubleEntry.Post(clearing, destination, command.Amount, command.TransferId, Description, now);
        posting.RecordCredit(destination.Id, now);

        await accounts.SaveAsync(clearing, cancellationToken);
        await accounts.SaveAsync(destination, cancellationToken);
        await postings.SaveAsync(posting, cancellationToken);
        await publisher.PublishAsync(
            new AccountCredited(credit.EntryId, credit.AccountId, credit.Amount, command.TransferId, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
