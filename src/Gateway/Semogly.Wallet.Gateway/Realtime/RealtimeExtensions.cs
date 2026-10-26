using Semogly.Wallet.Messaging;
using StackExchange.Redis;

namespace Semogly.Wallet.Gateway.Realtime;

internal static class RealtimeExtensions
{
    private const string RedisConnectionName = "redis";

    public static IHostApplicationBuilder AddRealtime(this IHostApplicationBuilder builder)
    {
        var redis = builder.Configuration.GetConnectionString(RedisConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{RedisConnectionName}' não configurada.");

        // Backplane: mensagens publicadas por uma instância chegam às conexões de todas
        builder.Services.AddSignalR()
            .AddStackExchangeRedis(redis, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal("gateway-signalr"));

        builder.AddMessaging("gateway", bus =>
        {
            bus.AddConsumer<TransferRequestedConsumer>();
            bus.AddConsumer<TransferCompletedConsumer>();
            bus.AddConsumer<TransferFailedConsumer>();
        });

        return builder;
    }
}
