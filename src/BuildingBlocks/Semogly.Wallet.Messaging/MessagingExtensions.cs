using MassTransit;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Semogly.Wallet.Messaging;

public static class MessagingExtensions
{
    /// <summary>Nome da connection string do RabbitMQ injetada pelo AppHost.</summary>
    public const string RabbitMqConnectionName = "rabbitmq";

    /// <summary>
    /// Configura MassTransit sobre RabbitMQ com Transactional Outbox/Inbox no <typeparamref name="TDbContext"/>:
    /// mensagens publicadas são gravadas na mesma transação das alterações de negócio e
    /// mensagens consumidas são deduplicadas pelo MessageId (idempotência).
    /// </summary>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null)
        where TDbContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(RabbitMqConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{RabbitMqConnectionName}' não configurada.");

        builder.Services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // Retry precisa envolver o outbox para que cada tentativa use uma transação nova
            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(connectionString));
                rabbit.ConfigureEndpoints(context);
            });
        });

        // Traces e métricas do MassTransit no dashboard do Aspire
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(DiagnosticHeaders.DefaultListenerName))
            .WithMetrics(metrics => metrics.AddMeter(InstrumentationOptions.MeterName));

        return builder;
    }
}
