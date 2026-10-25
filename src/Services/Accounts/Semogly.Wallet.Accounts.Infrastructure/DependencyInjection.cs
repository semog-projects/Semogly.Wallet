using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Accounts.Application.Abstractions;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.Accounts.Infrastructure.Caching;
using Semogly.Wallet.Accounts.Infrastructure.Messaging;
using Semogly.Wallet.Accounts.Infrastructure.Persistence;
using Semogly.Wallet.Messaging;

namespace Semogly.Wallet.Accounts.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Connection strings injetadas pelo AppHost do Aspire
        builder.AddNpgsqlDbContext<AccountsDbContext>("accounts-db");
        builder.AddRedisDistributedCache("redis");

        builder.Services.AddHybridCache(options => options.DefaultEntryOptions = new()
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(1),
        });

        builder.AddMessaging<AccountsDbContext>("accounts", bus =>
        {
            bus.AddConsumer<AccountDebitedConsumer>();
            bus.AddConsumer<AccountCreditedConsumer>();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AccountsDbContext>());
        builder.Services.AddScoped<IAccountRepository, AccountRepository>();
        builder.Services.AddScoped<IAccountReadService, CachedAccountReadService>();
        builder.Services.AddScoped<IAccountCache, HybridAccountCache>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }
}
