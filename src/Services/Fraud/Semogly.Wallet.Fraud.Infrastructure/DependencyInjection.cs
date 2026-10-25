using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Domain.Analysis;
using Semogly.Wallet.Fraud.Infrastructure.Activity;
using Semogly.Wallet.Fraud.Infrastructure.Messaging;
using Semogly.Wallet.Messaging;

namespace Semogly.Wallet.Fraud.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Connection string injetada pelo AppHost do Aspire
        builder.AddRedisClient("redis");

        var policy = builder.Configuration.GetSection(FraudPolicy.SectionName).Get<FraudPolicy>() ?? new FraudPolicy();
        builder.Services.AddSingleton(policy);

        builder.AddMessaging("fraud", bus =>
        {
            bus.AddConsumer<CheckTransferFraudConsumer>();
            bus.AddConsumer<TransferFailedConsumer>();
        });

        builder.Services.AddSingleton<IAccountActivityStore, RedisAccountActivityStore>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }
}
