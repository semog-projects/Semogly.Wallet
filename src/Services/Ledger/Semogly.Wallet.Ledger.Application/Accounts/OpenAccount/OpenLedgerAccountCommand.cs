using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.OpenAccount;

/// <summary>Abre no ledger a conta criada no serviço Accounts.</summary>
public sealed record OpenLedgerAccountCommand(Guid AccountId) : ICommand;

internal sealed class OpenLedgerAccountCommandHandler(
    ILedgerAccountRepository accounts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<OpenLedgerAccountCommand>
{
    public async Task<Result> HandleAsync(OpenLedgerAccountCommand command, CancellationToken cancellationToken)
    {
        // Idempotente: reprocessar o evento não duplica a conta
        if (await accounts.GetAsync(command.AccountId, cancellationToken) is not null)
        {
            return Result.Success();
        }

        await accounts.SaveAsync(
            LedgerAccount.Open(command.AccountId, LedgerAccountType.Customer, clock.GetUtcNow()),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
