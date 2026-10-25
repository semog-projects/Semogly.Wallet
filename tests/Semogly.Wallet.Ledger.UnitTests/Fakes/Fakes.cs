using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;

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

    /// <summary>Partidas dobradas: a soma de todos os saldos do livro deve ser zero.</summary>
    public decimal TotalBalance() => Streams.Keys.Sum(BalanceOf);

    public void Seed(LedgerAccount account) => SaveAsync(account, CancellationToken.None);
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
