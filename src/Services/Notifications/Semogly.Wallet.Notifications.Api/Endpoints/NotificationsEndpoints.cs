using System.ComponentModel.DataAnnotations;
using Semogly.Wallet.Notifications.Api.ErrorHandling;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Notifications.Api.Endpoints;

internal static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/accounts/{accountId:guid}", GetNotificationsAsync)
            .WithTags("Notifications")
            .WithName("GetNotifications")
            .WithSummary("Lista as notificações enviadas para a conta, da mais recente para a mais antiga")
            .WithDescription("Titular ou operador.")
            .RequireAuthorization()
            .RequireAccountAccess()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces<IReadOnlyList<NotificationResponse>>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GetNotificationsAsync(
        Guid accountId,
        IQueryHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>> handler,
        CancellationToken cancellationToken,
        [Range(1, 100)] int limit = 20)
    {
        var result = await handler.HandleAsync(new GetNotificationsQuery(accountId, limit), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
