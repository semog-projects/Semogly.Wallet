using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.Infrastructure.Persistence;

internal sealed class ContactRepository(NotificationsDbContext db) : IContactRepository
{
    public Task<Contact?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Contacts.FirstOrDefaultAsync(c => c.AccountId == accountId, cancellationToken);

    public void Add(Contact contact) => db.Contacts.Add(contact);
}

internal sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken) =>
        db.Notifications.AnyAsync(n => n.DeduplicationKey == deduplicationKey, cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}

internal sealed class NotificationReadService(NotificationsDbContext db) : INotificationReadService
{
    public async Task<IReadOnlyList<NotificationResponse>> GetByAccountAsync(
        Guid accountId, int limit, CancellationToken cancellationToken) =>
        await db.Notifications
            .AsNoTracking()
            .Where(n => n.AccountId == accountId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new NotificationResponse(
                n.Id, n.Type, n.Recipient, n.Subject, n.Body, n.Status, n.CreatedAt, n.SentAt))
            .ToListAsync(cancellationToken);
}
