using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Messaging;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Domain.Transfers;
using Semogly.Wallet.Transfers.Infrastructure.Messaging;
using Semogly.Wallet.Transfers.Infrastructure.Persistence;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.Transfers.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Connection string injetada pelo AppHost do Aspire
        builder.AddNpgsqlDbContext<TransfersDbContext>("transfers-db");

        builder.AddMessaging<TransfersDbContext>("transfers", bus =>
        {
            bus.AddSagaStateMachine<TransferStateMachine, TransferState>()
                .EntityFrameworkRepository(repository =>
                {
                    repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    repository.ExistingDbContext<TransfersDbContext>();
                    repository.UsePostgres();
                });

            bus.AddConsumer<TransferCompletedConsumer>();
            bus.AddConsumer<TransferFailedConsumer>();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransfersDbContext>());
        builder.Services.AddScoped<ITransferRepository, TransferRepository>();
        builder.Services.AddScoped<ITransferReadService, TransferReadService>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }
}
