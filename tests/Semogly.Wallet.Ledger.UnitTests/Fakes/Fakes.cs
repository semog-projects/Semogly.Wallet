using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;

namespace Semogly.Wallet.Ledger.UnitTests.Fakes;

/// <summary>Event store em memória: guarda os eventos por stream e reconstrói as contas a partir deles.</summary>
internal sealed class FakeLedgerAccountRepository : ILedgerAccountRepository
{
    public Dictionary<Guid, List<object>> Streams { get; } = [];

    public Task<LedgerAccount?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(Streams.TryGetValue(accountId, out var events) ? LedgerAccount.Rehydrate(events) : null);

    public Task SaveAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        if (!Streams.TryGetValue(account.Id, out var events))
        {
            Streams[account.Id] = events = [];
        }

        events.AddRange(account.PendingEvents);
        return Task.CompletedTask;
    }

    public decimal BalanceOf(Guid accountId) => LedgerAccount.Rehydrate(Streams[accountId]).Balance;

    /// <summary>Saldo em trânsito, somando as partições da conta Clearing.</summary>
    public decimal ClearingBalance() => Streams.Keys.Where(SystemAccounts.IsClearing).Sum(BalanceOf);

    /// <summary>Partidas dobradas: a soma de todos os saldos do livro deve ser zero.</summary>
    public decimal TotalBalance() => Streams.Keys.Sum(BalanceOf);

    public void Seed(LedgerAccount account) => SaveAsync(account, CancellationToken.None);
}

/// <summary>Streams das transferências em memória.</summary>
internal sealed class FakeTransferPostingRepository : ITransferPostingRepository
{
    public Dictionary<Guid, List<object>> Streams { get; } = [];

    public Task<TransferPosting> GetAsync(Guid transferId, CancellationToken cancellationToken) =>
        Task.FromResult(TransferPosting.Rehydrate(transferId, Streams.GetValueOrDefault(transferId) ?? []));

    public Task SaveAsync(TransferPosting posting, CancellationToken cancellationToken)
    {
        if (!Streams.TryGetValue(posting.Id, out var events))
        {
            Streams[posting.Id] = events = [];
        }

        events.AddRange(posting.PendingEvents);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDepositReceiptStore : IDepositReceiptStore
{
    public Dictionary<(Guid, string), DepositReceipt> Receipts { get; } = [];

    public Task<DepositReceipt?> FindAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(Receipts.GetValueOrDefault((accountId, idempotencyKey)));

    public Task AddAsync(DepositReceipt receipt, CancellationToken cancellationToken)
    {
        Receipts.Add((receipt.AccountId, receipt.IdempotencyKey), receipt);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
