using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.Transfers.Api.ErrorHandling;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.GetTransfer;

namespace Semogly.Wallet.Transfers.Api.Endpoints;

public sealed record CreateTransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    [Range(0.01, 1_000_000)] decimal Amount);

internal static class TransfersEndpoints
{
    private const string GetTransferRoute = "GetTransfer";
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapTransfersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", CreateTransferAsync)
            .WithName("CreateTransfer")
            .WithSummary("Solicita uma transferência entre contas")
            .WithDescription(
                "Processamento assíncrono via saga: retorna 202 e o status deve ser acompanhado em GET /transfers/{id}. " +
                "Repetir a requisição com a mesma Idempotency-Key devolve a transferência original.")
            .Produces<TransferResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", GetTransferAsync)
            .WithName(GetTransferRoute)
            .WithSummary("Consulta o status de uma transferência")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateTransferAsync(
        CreateTransferRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        ICommandHandler<CreateTransferCommand, TransferResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateTransferCommand(
                request.SourceAccountId,
                request.DestinationAccountId,
                request.Amount,
                idempotencyKey ?? string.Empty),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.AcceptedAtRoute(result.Value, GetTransferRoute, new { id = result.Value.Id })
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetTransferAsync(
        Guid id,
        IQueryHandler<GetTransferQuery, TransferResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTransferQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
