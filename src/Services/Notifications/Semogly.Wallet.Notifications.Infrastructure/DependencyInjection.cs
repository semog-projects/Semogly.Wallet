using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Messaging;
using Semogly.Wallet.Notifications.Application.Abstractions;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;
using Semogly.Wallet.Notifications.Infrastructure.Messaging;
using Semogly.Wallet.Notifications.Infrastructure.Persistence;
using Semogly.Wallet.Notifications.Infrastructure.Sending;

namespace Semogly.Wallet.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Connection string injetada pelo AppHost do Aspire
        builder.AddNpgsqlDbContext<NotificationsDbContext>("notifications-db");

        // Retry (100ms → 5s) e dead-letter (fila _error) vêm da configuração padrão do Messaging
        builder.AddMessaging<NotificationsDbContext>("notifications", bus =>
        {
            bus.AddConsumer<AccountCreatedConsumer>();
            bus.AddConsumer<TransferCompletedConsumer>();
            bus.AddConsumer<TransferFailedConsumer>();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        builder.Services.AddScoped<IContactRepository, ContactRepository>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<INotificationReadService, NotificationReadService>();
        builder.Services.AddScoped<INotificationSender, FakeEmailSender>();

        return builder;
    }
}
