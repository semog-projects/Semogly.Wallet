using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts;

public static class LedgerErrors
{
    public const string AccountNotFoundReason = "Conta não encontrada no ledger.";
    public const string InsufficientFundsReason = "Saldo insuficiente.";

    public static Error AccountNotFound(Guid accountId) =>
        Error.NotFound("Ledger.AccountNotFound", $"Conta {accountId} não encontrada no ledger.");
}
