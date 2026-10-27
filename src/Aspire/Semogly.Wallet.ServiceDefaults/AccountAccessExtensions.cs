using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Quem pode acessar uma conta: o titular (e-mail do token igual ao da conta) ou um operador (backoffice).
/// </summary>
public interface IAccountAccess
{
    /// <param name="accessToken">Token do próprio usuário, repassado ao Accounts para a consulta de posse.</param>
    Task<bool> CanAccessAsync(ClaimsPrincipal user, string? accessToken, Guid accountId, CancellationToken cancellationToken);
}

/// <summary>
/// Posse de conta entre serviços. O Accounts é o dono do vínculo conta → titular: os demais serviços
/// perguntam a ele (GET /accounts/{id}) com o token do próprio usuário, e o Accounts aplica a regra.
/// </summary>
public static class AccountAccessExtensions
{
    /// <summary>Nome do recurso do Accounts no AppHost (e no service discovery).</summary>
    public const string AccountsResource = "accounts-api";

    /// <summary>Nome do HttpClient usado na consulta de posse (substituível nos testes).</summary>
    public const string HttpClientName = "account-access";

    public const string AccountIdRouteParameter = "accountId";

    public static IHostApplicationBuilder AddAccountAccess(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient<IAccountAccess, AccountsServiceAccountAccess>(
            HttpClientName,
            client => client.BaseAddress = new Uri($"https+http://{AccountsResource}"));

        return builder;
    }

    /// <summary>A regra de posse, aplicada pelo Accounts sobre os próprios dados.</summary>
    public static bool IsOwnerOrOperator(this ClaimsPrincipal user, string ownerEmail)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsInRole(JwtValidationExtensions.OperatorRole)
            || string.Equals(user.FindFirstValue(JwtValidationExtensions.EmailClaim), ownerEmail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Token Bearer da requisição (o mesmo que o Gateway repassou ao serviço).</summary>
    public static string? BearerToken(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header)
            && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? header.Parameter
                : null;
    }

    /// <summary>Resposta para conta de outro titular.</summary>
    public static IResult Forbidden(Guid accountId) => TypedResults.Problem(
        title: "Account.Forbidden",
        detail: $"A conta '{accountId}' não pertence ao usuário autenticado.",
        statusCode: StatusCodes.Status403Forbidden);

    /// <summary>Exige acesso à conta do parâmetro de rota {accountId}: titular ou operador.</summary>
    public static RouteHandlerBuilder RequireAccountAccess(this RouteHandlerBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint
            .AddEndpointFilter(async (context, next) =>
            {
                var http = context.HttpContext;
                var accountId = Guid.Parse((string)http.Request.RouteValues[AccountIdRouteParameter]!);
                var access = http.RequestServices.GetRequiredService<IAccountAccess>();

                return await access.CanAccessAsync(http.User, http.BearerToken(), accountId, http.RequestAborted)
                    ? await next(context)
                    : Forbidden(accountId);
            })
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

internal sealed class AccountsServiceAccountAccess(HttpClient http, IMemoryCache cache) : IAccountAccess
{
    // A posse não muda (a conta não troca de titular): a resposta positiva pode ficar em cache.
    // A negativa não, porque a conta pode ser aberta logo depois.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user, string? accessToken, Guid accountId, CancellationToken cancellationToken)
    {
        if (user.IsInRole(JwtValidationExtensions.OperatorRole))
        {
            return true;
        }

        var email = user.FindFirstValue(JwtValidationExtensions.EmailClaim);
        if (email is null || accessToken is null)
        {
            return false;
        }

        var key = (nameof(AccountsServiceAccountAccess), email.ToUpperInvariant(), accountId);
        if (cache.TryGetValue(key, out _))
        {
            return true;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/accounts/{accountId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        cache.Set(key, true, CacheDuration);
        return true;
    }
}
