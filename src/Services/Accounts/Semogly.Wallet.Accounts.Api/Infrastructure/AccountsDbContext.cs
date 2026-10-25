using MassTransit;
using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Accounts.Api.Domain;

namespace Semogly.Wallet.Accounts.Api.Infrastructure;

public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(a => a.Id);
            account.Property(a => a.OwnerName).HasMaxLength(150);
            account.Property(a => a.Document).HasMaxLength(11);
            account.HasIndex(a => a.Document).IsUnique();
            account.Property(a => a.Email).HasMaxLength(254);
            account.Property(a => a.Currency).HasMaxLength(3);
            account.Property(a => a.Balance).HasPrecision(18, 2);
            account.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            account.Property(a => a.Version).IsRowVersion();
        });

        // Tabelas do Transactional Outbox/Inbox do MassTransit
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
