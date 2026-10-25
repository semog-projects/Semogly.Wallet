using System.ComponentModel.DataAnnotations;
using Semogly.Wallet.Accounts.Api.ErrorHandling;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetBalance;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Accounts.Api.Endpoints;

public sealed record CreateAccountRequest(
    [Required, StringLength(150, MinimumLength = 3)] string OwnerName,
    [Required, StringLength(14, MinimumLength = 11)] string Document,
    [Required, EmailAddress, StringLength(254)] string Email);

internal static class AccountsEndpoints
{
    private const string GetAccountRoute = "GetAccount";

    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/accounts").WithTags("Accounts");

        group.MapPost("/", CreateAccountAsync)
            .WithName("CreateAccount")
            .WithSummary("Abre uma nova conta na carteira")
            .Produces<AccountResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", GetAccountAsync)
            .WithName(GetAccountRoute)
            .WithSummary("Consulta os dados de uma conta")
            .Produces<AccountResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/balance", GetBalanceAsync)
            .WithName("GetBalance")
            .WithSummary("Consulta o saldo de uma conta")
            .Produces<BalanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        ICommandHandler<CreateAccountCommand, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateAccountCommand(request.OwnerName, request.Document, request.Email),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, GetAccountRoute, new { id = result.Value.Id })
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAccountAsync(
        Guid id,
        IQueryHandler<GetAccountQuery, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetBalanceAsync(
        Guid id,
        IQueryHandler<GetBalanceQuery, BalanceResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetBalanceQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
