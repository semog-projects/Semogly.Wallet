using Semogly.Wallet.Accounts.Application.Abstractions;
using Semogly.Wallet.Accounts.Domain.Accounts;

namespace Semogly.Wallet.Accounts.UnitTests.Fakes;

internal sealed class FakeAccountRepository : IAccountRepository
{
    public List<Account> Accounts { get; } = [];

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExistsByDocumentAsync(string document, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(a => a.Document == document));

    public void Add(Account account) => Accounts.Add(account);
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

internal sealed class FakeAccountCache : IAccountCache
{
    public List<Guid> Invalidated { get; } = [];

    public Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        Invalidated.Add(accountId);
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
