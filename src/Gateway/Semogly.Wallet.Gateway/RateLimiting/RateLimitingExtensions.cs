using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Semogly.Wallet.Gateway.RateLimiting;

internal static class RateLimitingExtensions
{
    /// <summary>Limite geral por usuário, aplicado às rotas da API.</summary>
    public const string PerUserPolicy = "per-user";

    /// <summary>Limite mais restrito para criação de transferências.</summary>
    public const string TransfersPolicy = "transfers";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(PerUserPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));

            // Token bucket: rajada de até 10 e reposição de 1 a cada 6s (~10/min); informa o Retry-After
            limiter.AddPolicy(TransfersPolicy, context => RateLimitPartition.GetTokenBucketLimiter(
                PartitionKey(context),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(6),
                }));

            limiter.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    // Usuário autenticado (sub do JWT); sem autenticação, o IP de origem
    private static string PartitionKey(HttpContext context) =>
        context.User.Identity is { IsAuthenticated: true, Name: { } user }
            ? $"user:{user}"
            : $"ip:{context.Connection.RemoteIpAddress}";
}
