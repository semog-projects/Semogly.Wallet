using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace Semogly.Wallet.Gateway.Bff;

/// <summary>
/// Sessão do BFF no servidor: o ticket (com os tokens do Keycloak) fica no Redis, criptografado pelo
/// Data Protection, e o cookie leva só a chave da sessão. Evita cookies grandes (fatiados) e tira os
/// tokens do navegador por completo; qualquer instância do Gateway encontra a sessão.
/// </summary>
internal sealed class RedisTicketStore(IDistributedCache cache, IDataProtectionProvider dataProtection, TimeProvider clock)
    : ITicketStore
{
    private const string KeyPrefix = "gateway:bff-session:";
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Semogly.Wallet.Gateway.BffSession");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // A sessão expira junto com o ticket (que por sua vez não passa da validade do token)
        var expiresAt = ticket.Properties.ExpiresUtc ?? clock.GetUtcNow().Add(DefaultLifetime);

        return cache.SetAsync(
            KeyPrefix + key,
            _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt });
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var data = await cache.GetAsync(KeyPrefix + key);
        return data is null ? null : TicketSerializer.Default.Deserialize(_protector.Unprotect(data));
    }

    public Task RemoveAsync(string key) => cache.RemoveAsync(KeyPrefix + key);
}

internal static class TicketStoreExtensions
{
    public static IHostApplicationBuilder AddBffSessionStore(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache("redis");
        builder.Services.AddSingleton<ITicketStore, RedisTicketStore>();
        builder.Services.AddOptions<CookieAuthenticationOptions>(BffDefaults.CookieScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);

        return builder;
    }
}
