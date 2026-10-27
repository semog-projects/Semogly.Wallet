using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Transfers;

/// <summary>Debita a origem de uma transferência, movendo o valor para a conta de trânsito (Clearing).</summary>
public sealed record DebitForTransferCommand(Guid TransferId, Guid AccountId, decimal Amount) : ICommand;

internal sealed class DebitForTransferCommandHandler(
    ILedgerAccountRepository accounts,
    ITransferPostingRepository postings,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DebitForTransferCommand>
{
    public const string Description = "Envio de transferência";

    public async Task<Result> HandleAsync(DebitForTransferCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var posting = await postings.GetAsync(command.TransferId, cancellationToken);

        // Cancelada por timeout da saga (o desfecho já foi informado) ou débito já lançado: nada a fazer
        if (!posting.CanDebit)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var source = await accounts.GetCustomerAccountAsync(command.AccountId, cancellationToken);

        // Recusas são desfechos de negócio: viram evento para a saga, não exceção
        var rejection = source is null ? LedgerErrors.AccountNotFoundReason
            : !source.CanDebit(command.Amount) ? LedgerErrors.InsufficientFundsReason
            : null;

        if (rejection is not null)
        {
            await publisher.PublishAsync(
                new AccountDebitRejected(command.TransferId, command.AccountId, rejection), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var clearing = await accounts.GetOrOpenSystemAccountAsync(SystemAccounts.Clearing, now, cancellationToken);
        var (debit, _) = DoubleEntry.Post(source!, clearing, command.Amount, command.TransferId, Description, now);
        posting.RecordDebit(source!.Id, command.Amount, now);

        await accounts.SaveAsync(source, cancellationToken);
        await accounts.SaveAsync(clearing, cancellationToken);
        await postings.SaveAsync(posting, cancellationToken);
        await publisher.PublishAsync(
            new AccountDebited(debit.EntryId, debit.AccountId, debit.Amount, command.TransferId, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
