using Semogly.Wallet.Notifications.Application.Abstractions;
using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.Application.Notifications;

/// <summary>Envia e registra notificações, ignorando as que já foram enviadas.</summary>
internal sealed class NotificationDispatcher(
    INotificationRepository notifications,
    INotificationSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task DispatchAsync(IEnumerable<Notification> pending, CancellationToken cancellationToken)
    {
        foreach (var notification in pending)
        {
            if (await notifications.ExistsAsync(notification.DeduplicationKey, cancellationToken))
            {
                continue;
            }

            // Falha no envio lança exceção: a transação é desfeita e o MassTransit tenta de novo
            // (retry); esgotadas as tentativas, a mensagem vai para a fila _error (dead-letter)
            await sender.SendAsync(notification, cancellationToken);
            notification.MarkSent(clock.GetUtcNow());
            notifications.Add(notification);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
