namespace Semogly.Wallet.Notifications.Domain.Notifications;

public enum NotificationType
{
    Welcome,
    TransferSent,
    TransferReceived,
    TransferFailed,
}

public enum NotificationStatus
{
    Pending,
    Sent,
}

/// <summary>Mensagem enviada ao titular. Fica registrada como histórico do que foi comunicado.</summary>
public sealed class Notification
{
    // Construtor exigido pelo EF Core
    private Notification() { }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public NotificationType Type { get; private set; }

    /// <summary>
    /// Chave determinística (tipo + evento de origem + conta): impede notificação duplicada e
    /// é repassada ao provedor como chave de idempotência.
    /// </summary>
    public string DeduplicationKey { get; private set; } = null!;

    public string Recipient { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static Notification Create(
        Guid accountId,
        NotificationType type,
        Guid sourceEventId,
        string recipient,
        NotificationContent content,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(content);

        return new Notification
        {
            Id = Guid.CreateVersion7(now),
            AccountId = accountId,
            Type = type,
            DeduplicationKey = BuildKey(type, sourceEventId, accountId),
            Recipient = recipient,
            Subject = content.Subject,
            Body = content.Body,
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        };
    }

    public static string BuildKey(NotificationType type, Guid sourceEventId, Guid accountId) =>
        $"{type}:{sourceEventId:N}:{accountId:N}";

    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
    }
}
