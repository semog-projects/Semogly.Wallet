using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts;

public static class AccountErrors
{
    public static readonly Error DocumentAlreadyRegistered =
        Error.Conflict("Account.DocumentAlreadyRegistered", "Já existe uma conta para este documento.");

    public static Error NotFound(Guid accountId) =>
        Error.NotFound("Account.NotFound", $"Conta {accountId} não encontrada.");
}
