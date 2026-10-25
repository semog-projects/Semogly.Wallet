using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.UnitTests.Fakes;

internal sealed class FakeTransferRepository : ITransferRepository
{
    public List<Transfer> Transfers { get; } = [];

    public Task<Transfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.FirstOrDefault(t => t.Id == id));

    public Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(Transfers.FirstOrDefault(t => t.IdempotencyKey == idempotencyKey));

    public void Add(Transfer transfer) => Transfers.Add(transfer);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>Executado no save para simular efeitos do banco (ex.: violação de unicidade).</summary>
    public Action? OnSave { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        OnSave?.Invoke();
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
