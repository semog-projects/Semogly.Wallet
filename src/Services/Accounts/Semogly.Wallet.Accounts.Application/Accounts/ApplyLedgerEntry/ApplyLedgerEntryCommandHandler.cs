using Semogly.Wallet.Accounts.Application.Abstractions;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;

internal sealed class ApplyLedgerEntryCommandHandler(
    IAccountRepository accounts,
    IAccountCache cache,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ApplyLedgerEntryCommand>
{
    public async Task<Result> HandleAsync(ApplyLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(command.AccountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure(AccountErrors.NotFound(command.AccountId));
        }

        var now = clock.GetUtcNow();

        if (command.Type == LedgerEntryType.Debit)
        {
            account.ApplyDebit(command.Amount, now);
        }
        else
        {
            account.ApplyCredit(command.Amount, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(account.Id, cancellationToken);

        return Result.Success();
    }
}
