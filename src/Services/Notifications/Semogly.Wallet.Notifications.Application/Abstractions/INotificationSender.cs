using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.Application.Abstractions;

/// <summary>
/// Provedor de envio (e-mail, SMS, push). Plugável: a Application não conhece o provedor concreto.
/// Implementações devem usar <see cref="Notification.DeduplicationKey"/> como chave de idempotência.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
