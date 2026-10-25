using MassTransit;
using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Notifications.Application.Abstractions;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Notification> Notifications => Set<Notification>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contact>(contact =>
        {
            contact.ToTable("contacts");
            contact.HasKey(c => c.AccountId);
            contact.Property(c => c.Name).HasMaxLength(150);
            contact.Property(c => c.Email).HasMaxLength(254);
            contact.Ignore(c => c.FirstName);
        });

        modelBuilder.Entity<Notification>(notification =>
        {
            notification.ToTable("notifications");
            notification.HasKey(n => n.Id);
            notification.Property(n => n.DeduplicationKey).HasMaxLength(120);
            notification.HasIndex(n => n.DeduplicationKey).IsUnique();
            notification.HasIndex(n => n.AccountId);
            notification.Property(n => n.Type).HasConversion<string>().HasMaxLength(30);
            notification.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            notification.Property(n => n.Recipient).HasMaxLength(254);
            notification.Property(n => n.Subject).HasMaxLength(200);
            notification.Property(n => n.Body).HasMaxLength(2000);
        });

        // Tabelas do Transactional Outbox/Inbox do MassTransit (o Inbox evita e-mail duplicado)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
