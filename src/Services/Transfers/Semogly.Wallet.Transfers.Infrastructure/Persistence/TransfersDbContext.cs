using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Domain.Transfers;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

public sealed class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException(ex.InnerException.Message, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transfer>(transfer =>
        {
            transfer.ToTable("transfers");
            transfer.HasKey(t => t.Id);
            transfer.Property(t => t.Amount).HasPrecision(18, 2);
            transfer.Property(t => t.IdempotencyKey).HasMaxLength(Transfer.IdempotencyKeyMaxLength);
            transfer.HasIndex(t => t.IdempotencyKey).IsUnique();
            transfer.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            transfer.Property(t => t.FailureReason).HasMaxLength(500);
            transfer.Property(t => t.Version).IsRowVersion();
        });

        // Estado da saga na mesma base: transição de estado e mensagens publicadas na mesma transação
        new TransferStateMap().Configure(modelBuilder);

        // Tabelas do Transactional Outbox/Inbox do MassTransit
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
