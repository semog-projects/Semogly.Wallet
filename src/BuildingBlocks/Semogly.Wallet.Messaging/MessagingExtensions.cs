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
    /// <param name="builder">Builder do host.</param>
    /// <param name="serviceName">Prefixo das filas do serviço (ver <see cref="AddMessagingCore"/>).</param>
    /// <param name="configure">Registro de consumers e sagas.</param>
    /// <param name="configureBus">Ajustes no barramento do RabbitMQ (ex.: agendador de mensagens).</param>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configure = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
        where TDbContext : DbContext =>
        builder.AddMessagingCore(
            serviceName,
            bus =>
            {
                bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });

                configure?.Invoke(bus);
            },
            (context, endpoint) => endpoint.UseEntityFrameworkOutbox<TDbContext>(context),
            configureBus);

    /// <summary>
    /// Variante para serviços sem banco relacional: usa o in-memory outbox, que só publica as mensagens
    /// depois que o consumer termina com sucesso. A idempotência fica a cargo do próprio consumer.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configure = null) =>
        builder.AddMessagingCore(
            serviceName,
            bus => configure?.Invoke(bus),
            (context, endpoint) => endpoint.UseInMemoryOutbox(context));

    private static IHostApplicationBuilder AddMessagingCore(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<IBusRegistrationConfigurator> configure,
        Action<IRegistrationContext, IReceiveEndpointConfigurator> configureOutbox,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var connectionString = builder.Configuration.GetConnectionString(RabbitMqConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{RabbitMqConnectionName}' não configurada.");

        builder.Services.AddMassTransit(bus =>
        {
            // Filas prefixadas pelo serviço: consumers homônimos em serviços diferentes (ex.: TransferFailedConsumer)
            // precisam de filas próprias para que cada serviço receba sua cópia do evento
            bus.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceName, includeNamespace: false));

            // Retry precisa envolver o outbox para que cada tentativa comece do zero
            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000, 5_000));
                configureOutbox(context, endpoint);
            });

            configure(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(connectionString));
                configureBus?.Invoke(context, rabbit);
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
