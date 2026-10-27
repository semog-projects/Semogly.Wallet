using System.ComponentModel.DataAnnotations;
using Semogly.Wallet.Ledger.Api.ErrorHandling;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Ledger.Api.Endpoints;

public sealed record DepositRequest([Range(0.01, 1_000_000)] decimal Amount);

internal static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/ledger/accounts/{accountId:guid}")
            .WithTags("Ledger")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/deposits", DepositAsync)
            .WithName("Deposit")
            .WithSummary("Registra um depósito (entrada de dinheiro externo) na conta")
            .WithDescription("Exige o papel operator.")
            .RequireAuthorization(JwtValidationExtensions.OperatorPolicy)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces<DepositResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/statement", GetStatementAsync)
            .WithName("GetStatement")
            .WithSummary("Consulta o extrato da conta, do lançamento mais recente para o mais antigo")
            .WithDescription("Titular ou operador.")
            .RequireAccountAccess()
            .Produces<StatementResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/ledger/book", GetBookAsync)
            .WithTags("Ledger")
            .WithName("GetBook")
            .WithSummary("Visão do livro-razão: saldos de CashIn, Clearing e clientes (backoffice)")
            .WithDescription("Exige o papel operator. Total diferente de zero indica inconsistência contábil.")
            .RequireAuthorization(JwtValidationExtensions.OperatorPolicy)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces<LedgerBookResponse>();

        return app;
    }

    private static async Task<IResult> DepositAsync(
        Guid accountId,
        DepositRequest request,
        ICommandHandler<DepositCommand, DepositResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DepositCommand(accountId, request.Amount), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetBookAsync(
        IQueryHandler<GetBookQuery, LedgerBookResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetBookQuery(), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetStatementAsync(
        Guid accountId,
        IQueryHandler<GetStatementQuery, StatementResponse> handler,
        CancellationToken cancellationToken,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var result = await handler.HandleAsync(new GetStatementQuery(accountId, page, pageSize), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
