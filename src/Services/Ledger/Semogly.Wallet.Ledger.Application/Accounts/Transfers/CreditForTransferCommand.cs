using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Transfers;

/// <summary>
/// Credita o destino (ou estorna a origem) de uma transferência, retirando o valor da conta de trânsito (Clearing).
/// </summary>
public sealed record CreditForTransferCommand(Guid TransferId, Guid AccountId, decimal Amount) : ICommand;

internal sealed class CreditForTransferCommandHandler(
    ILedgerAccountRepository accounts,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreditForTransferCommand>
{
    public const string Description = "Recebimento de transferência";

    public async Task<Result> HandleAsync(CreditForTransferCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
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

        await accounts.SaveAsync(clearing, cancellationToken);
        await accounts.SaveAsync(destination, cancellationToken);
        await publisher.PublishAsync(
            new AccountCredited(credit.EntryId, credit.AccountId, credit.Amount, command.TransferId, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
