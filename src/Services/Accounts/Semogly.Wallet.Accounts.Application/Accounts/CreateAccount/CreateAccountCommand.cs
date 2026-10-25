using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(string OwnerName, string Document, string Email) : ICommand<AccountResponse>;
