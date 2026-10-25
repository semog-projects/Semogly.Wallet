using Semogly.Wallet.Notifications.Domain.Notifications;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Notifications.Application.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Recipient,
    string Subject,
    string Body,
    NotificationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

/// <summary>Lado de leitura (CQRS): projeção direta para o DTO.</summary>
public interface INotificationReadService
{
    Task<IReadOnlyList<NotificationResponse>> GetByAccountAsync(Guid accountId, int limit, CancellationToken cancellationToken);
}

/// <summary>Notificações enviadas para a conta, da mais recente para a mais antiga.</summary>
public sealed record GetNotificationsQuery(Guid AccountId, int Limit) : IQuery<IReadOnlyList<NotificationResponse>>;

internal sealed class GetNotificationsQueryHandler(INotificationReadService readService)
    : IQueryHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<Result<IReadOnlyList<NotificationResponse>>> HandleAsync(
        GetNotificationsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readService.GetByAccountAsync(query.AccountId, query.Limit, cancellationToken));
}
