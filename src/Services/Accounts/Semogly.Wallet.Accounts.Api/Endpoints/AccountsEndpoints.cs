using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Semogly.Wallet.Accounts.Api.ErrorHandling;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetBalance;
using Semogly.Wallet.Accounts.Application.Accounts.GetMyAccounts;
using Semogly.Wallet.Accounts.Application.Accounts.LookupAccount;
using Semogly.Wallet.Accounts.Application.Accounts.SearchAccounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Pagination;

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
        // Todas as rotas exigem JWT: o próprio serviço valida o token repassado pelo Gateway
        var group = app.MapGroup("/accounts")
            .WithTags("Accounts")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateAccountAsync)
            .WithName("CreateAccount")
            .WithSummary("Abre uma nova conta na carteira")
            .WithDescription("Cliente abre conta apenas no próprio e-mail (o do token); operador, para qualquer titular.")
            .Produces<AccountResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", SearchAccountsAsync)
            .WithName("SearchAccounts")
            .WithSummary("Busca contas por nome, e-mail ou CPF (backoffice)")
            .WithDescription("Exige o papel operator. Sem termo, lista as contas mais recentes.")
            .RequireAuthorization(JwtValidationExtensions.OperatorPolicy)
            .Produces<PagedResponse<AccountResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/lookup", LookupAccountAsync)
            .WithName("LookupAccount")
            .WithSummary("Encontra a conta de destino pelo e-mail do titular (chave de transferência)")
            .WithDescription("Retorna apenas o id e o nome mascarado, para o pagador confirmar o destinatário.")
            .Produces<AccountLookupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me", GetMyAccountsAsync)
            .WithName("GetMyAccounts")
            .WithSummary("Contas do titular autenticado (e-mail do token)")
            .Produces<IReadOnlyList<AccountResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetAccountAsync)
            .WithName(GetAccountRoute)
            .WithSummary("Consulta os dados de uma conta")
            .WithDescription("Titular ou operador. Os demais serviços consultam esta rota para verificar a posse da conta.")
            .Produces<AccountResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/balance", GetBalanceAsync)
            .WithName("GetBalance")
            .WithSummary("Consulta o saldo de uma conta")
            .WithDescription("Titular ou operador.")
            .Produces<BalanceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CreateAccountCommand, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        if (!user.IsOwnerOrOperator(request.Email.Trim()))
        {
            return TypedResults.Problem(
                title: "Account.ForeignEmail",
                detail: "Cliente só pode abrir conta no próprio e-mail.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await handler.HandleAsync(
            new CreateAccountCommand(request.OwnerName, request.Document, request.Email),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, GetAccountRoute, new { id = result.Value.Id })
            : result.Error.ToProblem();
    }

    private static async Task<IResult> SearchAccountsAsync(
        IQueryHandler<SearchAccountsQuery, PagedResponse<AccountResponse>> handler,
        CancellationToken cancellationToken,
        [StringLength(100)] string? search = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var result = await handler.HandleAsync(new SearchAccountsQuery(search, page, pageSize), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> LookupAccountAsync(
        [Required, EmailAddress, StringLength(254)] string email,
        IQueryHandler<LookupAccountQuery, AccountLookupResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LookupAccountQuery(email), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetMyAccountsAsync(
        ClaimsPrincipal user,
        IQueryHandler<GetMyAccountsQuery, IReadOnlyList<AccountResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetMyAccountsQuery(user.FindFirstValue(JwtValidationExtensions.EmailClaim)),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAccountAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetAccountQuery, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        return user.IsOwnerOrOperator(result.Value.Email)
            ? TypedResults.Ok(result.Value)
            : AccountAccessExtensions.Forbidden(id);
    }

    private static async Task<IResult> GetBalanceAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetAccountQuery, AccountResponse> accountHandler,
        IQueryHandler<GetBalanceQuery, BalanceResponse> handler,
        CancellationToken cancellationToken)
    {
        // Titular a partir dos dados da conta (leitura em cache)
        var account = await accountHandler.HandleAsync(new GetAccountQuery(id), cancellationToken);

        if (account.IsSuccess && !user.IsOwnerOrOperator(account.Value.Email))
        {
            return AccountAccessExtensions.Forbidden(id);
        }

        var result = await handler.HandleAsync(new GetBalanceQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
