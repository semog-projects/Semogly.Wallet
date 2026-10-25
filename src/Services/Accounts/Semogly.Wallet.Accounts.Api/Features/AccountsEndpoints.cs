using Semogly.Wallet.Accounts.Api.Features.CreateAccount;
using Semogly.Wallet.Accounts.Api.Features.GetAccount;
using Semogly.Wallet.Accounts.Api.Features.GetBalance;

namespace Semogly.Wallet.Accounts.Api.Features;

internal static class AccountsEndpoints
{
    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/accounts")
            .WithTags("Accounts")
            .MapCreateAccount()
            .MapGetAccount()
            .MapGetBalance();

        return app;
    }
}
