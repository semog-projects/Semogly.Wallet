using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;

public enum LedgerEntryType
{
    Debit,
    Credit,
}

/// <summary>Aplica na projeção de saldo um lançamento efetivado pelo Ledger.</summary>
public sealed record ApplyLedgerEntryCommand(Guid AccountId, decimal Amount, LedgerEntryType Type) : ICommand;
