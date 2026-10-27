using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
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
    private const string ConnectionName = "transfers-db";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Connection string injetada pelo AppHost do Aspire
        builder.AddNpgsqlDbContext<TransfersDbContext>(ConnectionName);

        builder.Services.AddSingleton(
            builder.Configuration.GetSection(TransferSagaOptions.SectionName).Get<TransferSagaOptions>()
            ?? new TransferSagaOptions());
        builder.AddMessageScheduler();

        builder.AddMessaging<TransfersDbContext>(
            "transfers",
            bus =>
            {
                // Timeouts da saga: o agendamento é publicado ao Quartz, que o guarda no Postgres e o entrega no prazo
                bus.AddPublishMessageScheduler();
                bus.AddQuartzConsumers();

                bus.AddSagaStateMachine<TransferStateMachine, TransferState>()
                    .EntityFrameworkRepository(repository =>
                    {
                        repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
                        repository.ExistingDbContext<TransfersDbContext>();
                        repository.UsePostgres();
                    });

                bus.AddConsumer<TransferCompletedConsumer>();
                bus.AddConsumer<TransferFailedConsumer>();
            },
            (_, rabbit) => rabbit.UsePublishMessageScheduler());

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransfersDbContext>());
        builder.Services.AddScoped<ITransferRepository, TransferRepository>();
        builder.Services.AddScoped<ITransferReadService, TransferReadService>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }

    /// <summary>
    /// Quartz com armazenamento no Postgres do serviço (tabelas criadas por migration) e em cluster: com várias
    /// instâncias, cada agendamento dispara uma única vez, e os vencidos durante uma parada disparam na volta.
    /// </summary>
    private static void AddMessageScheduler(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' não configurada.");

        builder.Services.AddQuartz(quartz =>
        {
            quartz.SchedulerName = "transfers-scheduler";
            quartz.SchedulerId = "AUTO";
            quartz.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UsePostgres(connectionString);
                store.UseSystemTextJsonSerializer();
                store.UseClustering();
            });
        });

        // Só começa a disparar depois que o barramento está pronto para receber as mensagens agendadas
        builder.Services.Configure<MassTransitHostOptions>(options => options.WaitUntilStarted = true);
        builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
    }
}
