using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Notifications.Application.Contacts;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Application.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<NotificationDispatcher>();

        services.AddScoped<ICommandHandler<RegisterContactCommand>, RegisterContactCommandHandler>();
        services.AddScoped<ICommandHandler<NotifyTransferCompletedCommand>, NotifyTransferCompletedCommandHandler>();
        services.AddScoped<ICommandHandler<NotifyTransferFailedCommand>, NotifyTransferFailedCommandHandler>();
        services.AddScoped<IQueryHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>>, GetNotificationsQueryHandler>();

        return services;
    }
}
