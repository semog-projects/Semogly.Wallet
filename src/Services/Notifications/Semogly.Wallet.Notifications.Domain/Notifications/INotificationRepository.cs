namespace Semogly.Wallet.Notifications.Domain.Notifications;

public interface INotificationRepository
{
    Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken);

    void Add(Notification notification);
}
