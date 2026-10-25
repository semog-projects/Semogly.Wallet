using System.Globalization;
using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Domain.Analysis;
using StackExchange.Redis;

namespace Semogly.Wallet.Fraud.Infrastructure.Activity;

/// <summary>
/// Contadores no Redis:
/// <list type="bullet">
/// <item><c>fraud:daily:{conta}:{data}</c> (hash TransferId → centavos), para o limite diário;</item>
/// <item><c>fraud:velocity:{conta}</c> (sorted set TransferId → timestamp), para a janela deslizante do velocity.</item>
/// </list>
/// Indexar por TransferId torna o registro idempotente em caso de reentrega da mensagem.
/// </summary>
internal sealed class RedisAccountActivityStore(IConnectionMultiplexer redis, FraudPolicy policy) : IAccountActivityStore
{
    private const int Approved = 0;
    private const int DailyLimitExceeded = 1;
    private const int VelocityExceeded = 2;

    // Verifica e registra em um único passo atômico: análises simultâneas da mesma conta não furam os limites
    private const string TryRegisterScript = """
        local daily, velocity = KEYS[1], KEYS[2]
        local transferId, amount, dailyLimit = ARGV[1], tonumber(ARGV[2]), tonumber(ARGV[3])
        local now, window, maxCount, dailyTtl = tonumber(ARGV[4]), tonumber(ARGV[5]), tonumber(ARGV[6]), tonumber(ARGV[7])

        if redis.call('HEXISTS', daily, transferId) == 1 then
            return 0
        end

        local used = 0
        for _, value in ipairs(redis.call('HVALS', daily)) do
            used = used + tonumber(value)
        end
        if used + amount > dailyLimit then
            return 1
        end

        redis.call('ZREMRANGEBYSCORE', velocity, '-inf', now - window)
        if redis.call('ZCARD', velocity) >= maxCount then
            return 2
        end

        redis.call('HSET', daily, transferId, amount)
        redis.call('EXPIRE', daily, dailyTtl)
        redis.call('ZADD', velocity, now, transferId)
        redis.call('PEXPIRE', velocity, window)
        return 0
        """;

    // Margem além das 24h para cobrir a diferença de fuso
    private static readonly TimeSpan DailyKeyTtl = TimeSpan.FromHours(48);

    public async Task<FraudRejection?> TryRegisterAsync(
        Guid accountId, Guid transferId, decimal amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = (int)await redis.GetDatabase().ScriptEvaluateAsync(
            TryRegisterScript,
            [DailyKey(accountId, now), VelocityKey(accountId)],
            [
                transferId.ToString(),
                ToCents(amount),
                ToCents(policy.DailyLimit),
                now.ToUnixTimeMilliseconds(),
                (long)policy.VelocityWindow.TotalMilliseconds,
                policy.MaxTransfersPerWindow,
                (long)DailyKeyTtl.TotalSeconds,
            ]);

        return result switch
        {
            Approved => null,
            DailyLimitExceeded => FraudRejection.DailyLimitExceeded,
            VelocityExceeded => FraudRejection.VelocityExceeded,
            _ => throw new InvalidOperationException($"Resultado inesperado do script antifraude: {result}"),
        };
    }

    public Task ReleaseAsync(Guid accountId, Guid transferId, DateTimeOffset now, CancellationToken cancellationToken) =>
        redis.GetDatabase().HashDeleteAsync(DailyKey(accountId, now), transferId.ToString());

    public async Task<decimal> GetDailyUsageAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var values = await redis.GetDatabase().HashValuesAsync(DailyKey(accountId, now));
        return values.Sum(v => (long)v) / 100m;
    }

    private RedisKey DailyKey(Guid accountId, DateTimeOffset now) =>
        $"fraud:daily:{accountId}:{policy.BusinessDate(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

    private static RedisKey VelocityKey(Guid accountId) => $"fraud:velocity:{accountId}";

    // Centavos inteiros evitam erro de arredondamento de ponto flutuante no Lua
    private static long ToCents(decimal amount) => (long)decimal.Round(amount * 100m);
}
