using System.ComponentModel.DataAnnotations;
using Semogly.Wallet.Ledger.Api.ErrorHandling;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Ledger.Api.Endpoints;

public sealed record DepositRequest([Range(0.01, 1_000_000)] decimal Amount);

internal static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/ledger/accounts/{accountId:guid}").WithTags("Ledger");

        group.MapPost("/deposits", DepositAsync)
            .WithName("Deposit")
            .WithSummary("Registra um depósito (entrada de dinheiro externo) na conta")
            .Produces<DepositResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/statement", GetStatementAsync)
            .WithName("GetStatement")
            .WithSummary("Consulta o extrato da conta, do lançamento mais recente para o mais antigo")
            .Produces<StatementResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

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
