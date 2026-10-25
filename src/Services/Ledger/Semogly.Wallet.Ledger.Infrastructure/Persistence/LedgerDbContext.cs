using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Semogly.Wallet.Ledger.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelo Transactional Outbox/Inbox do MassTransit. Os dados de negócio
/// ficam no event store do Marten, na mesma base e na mesma transação (ver <see cref="LedgerUnitOfWork"/>).
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
