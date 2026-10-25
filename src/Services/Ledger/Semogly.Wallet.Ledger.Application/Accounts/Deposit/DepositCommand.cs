using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Deposit;

/// <summary>Entrada de dinheiro externo (simula um PIX/boleto recebido), com contrapartida na conta CashIn.</summary>
public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<DepositResponse>;

public sealed record DepositResponse(Guid EntryId, Guid AccountId, decimal Amount, decimal BalanceAfter, DateTimeOffset OccurredAt);

internal sealed class DepositCommandHandler(
    ILedgerAccountRepository accounts,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DepositCommand, DepositResponse>
{
    public const string Description = "Depósito";

    public async Task<Result<DepositResponse>> HandleAsync(DepositCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var account = await accounts.GetCustomerAccountAsync(command.AccountId, cancellationToken);

        if (account is null)
        {
            return LedgerErrors.AccountNotFound(command.AccountId);
        }

        var cashIn = await accounts.GetOrOpenSystemAccountAsync(SystemAccounts.CashIn, now, cancellationToken);
        var (_, credit) = DoubleEntry.Post(cashIn, account, command.Amount, null, Description, now);

        await accounts.SaveAsync(cashIn, cancellationToken);
        await accounts.SaveAsync(account, cancellationToken);
        await publisher.PublishAsync(
            new AccountCredited(credit.EntryId, credit.AccountId, credit.Amount, null, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DepositResponse(credit.EntryId, credit.AccountId, credit.Amount, credit.BalanceAfter, now);
    }
}
