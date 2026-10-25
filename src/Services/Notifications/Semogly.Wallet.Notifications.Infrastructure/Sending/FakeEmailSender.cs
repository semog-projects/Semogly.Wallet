using Microsoft.Extensions.Logging;
using Semogly.Wallet.Notifications.Application.Abstractions;
using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.Infrastructure.Sending;

/// <summary>
/// Provedor fake para o MVP: registra o e-mail em log estruturado (visível no dashboard do Aspire).
/// Para produção, basta outra implementação de <see cref="INotificationSender"/> (SES, SendGrid etc.).
/// </summary>
internal sealed partial class FakeEmailSender(ILogger<FakeEmailSender> logger) : INotificationSender
{
    /// <summary>Destinatários deste domínio simulam falha do provedor, para exercitar retry e dead-letter.</summary>
    public const string FailingDomain = "@falha.test";

    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.Recipient.EndsWith(FailingDomain, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Provedor de e-mail indisponível para {notification.Recipient}.");
        }

        LogEmailSent(notification.Recipient, notification.Subject, notification.Type, notification.DeduplicationKey, notification.Body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "E-mail enviado para {Recipient}: {Subject} [{Type}, chave {IdempotencyKey}] {Body}")]
    private partial void LogEmailSent(string recipient, string subject, NotificationType type, string idempotencyKey, string body);
}
