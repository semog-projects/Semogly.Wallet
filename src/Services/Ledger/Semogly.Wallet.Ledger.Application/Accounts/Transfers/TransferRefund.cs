using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;

namespace Semogly.Wallet.Ledger.Application.Accounts.Transfers;

/// <summary>Estorno: o valor debitado volta da conta de trânsito (Clearing) para a origem.</summary>
internal static class TransferRefund
{
    public const string Description = "Estorno de transferência";

    public static async Task PostAsync(
        ILedgerAccountRepository accounts,
        ITransferPostingRepository postings,
        IEventPublisher publisher,
        TransferPosting posting,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var source = await accounts.GetCustomerAccountAsync(posting.SourceAccountId!.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Conta de origem {posting.SourceAccountId} não encontrada para o estorno.");
        var clearing = await accounts.GetOrOpenSystemAccountAsync(SystemAccounts.Clearing, now, cancellationToken);

        var (_, credit) = DoubleEntry.Post(clearing, source, posting.Amount, posting.Id, Description, now);
        posting.RecordRefund(now);

        await accounts.SaveAsync(clearing, cancellationToken);
        await accounts.SaveAsync(source, cancellationToken);
        await postings.SaveAsync(posting, cancellationToken);

        // Mesmo evento de um crédito comum: o Accounts atualiza o saldo e a saga conclui o estorno
        await publisher.PublishAsync(
            new AccountCredited(credit.EntryId, credit.AccountId, credit.Amount, posting.Id, now), cancellationToken);
    }
}
