using Semogly.Wallet.Accounts.Application.Abstractions;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.Contracts.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accounts,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CreateAccountCommand, AccountResponse>
{
    public async Task<Result<AccountResponse>> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        var account = Account.Open(command.OwnerName, command.Document, command.Email, clock.GetUtcNow());

        if (await accounts.ExistsByDocumentAsync(account.Document, cancellationToken))
        {
            return AccountErrors.DocumentAlreadyRegistered;
        }

        accounts.Add(account);

        await publisher.PublishAsync(
            new AccountCreated(account.Id, account.OwnerName, account.Email, account.Currency, account.CreatedAt),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(account);
    }
}
