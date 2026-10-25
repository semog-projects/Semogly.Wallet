using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Ledger.Domain.Accounts;

/// <summary>
/// Partidas dobradas: todo valor que sai de uma conta entra em outra. Débito e crédito
/// são sempre gerados juntos e com o mesmo valor, então a soma dos saldos do livro é zero.
/// </summary>
public static class DoubleEntry
{
    public static (FundsDebited Debit, FundsCredited Credit) Post(
        LedgerAccount from,
        LedgerAccount to,
        decimal amount,
        Guid? transferId,
        string description,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from.Id == to.Id)
        {
            throw new DomainException("Débito e crédito devem ocorrer em contas diferentes.");
        }

        return (from.Debit(amount, transferId, description, now), to.Credit(amount, transferId, description, now));
    }
}
