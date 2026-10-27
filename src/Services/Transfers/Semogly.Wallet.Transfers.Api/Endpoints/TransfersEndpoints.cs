using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.Transfers.Api.ErrorHandling;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.GetTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.ListTransfers;
using Semogly.Wallet.Transfers.Domain.Transfers;

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
        var group = app.MapGroup("/transfers")
            .WithTags("Transfers")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", CreateTransferAsync)
            .WithName("CreateTransfer")
            .WithSummary("Solicita uma transferência entre contas")
            .WithDescription(
                "Processamento assíncrono via saga: retorna 202 e o status deve ser acompanhado em GET /transfers/{id}. " +
                "Repetir a requisição com a mesma Idempotency-Key devolve a transferência original. " +
                "A conta de origem precisa ser do usuário autenticado (ou ele ser operador).")
            .Produces<TransferResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", ListTransfersAsync)
            .WithName("ListTransfers")
            .WithSummary("Lista transferências com filtros (backoffice)")
            .WithDescription("Exige o papel operator. A conta filtra tanto pela origem quanto pelo destino.")
            .RequireAuthorization(JwtValidationExtensions.OperatorPolicy)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces<PagedResponse<TransferResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetTransferAsync)
            .WithName(GetTransferRoute)
            .WithSummary("Consulta o status de uma transferência")
            .WithDescription("Titular da conta de origem ou de destino, ou operador.")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateTransferAsync(
        CreateTransferRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        HttpContext context,
        IAccountAccess access,
        ICommandHandler<CreateTransferCommand, TransferResponse> handler,
        CancellationToken cancellationToken)
    {
        if (!await access.CanAccessAsync(context.User, context.BearerToken(), request.SourceAccountId, cancellationToken))
        {
            return AccountAccessExtensions.Forbidden(request.SourceAccountId);
        }

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

    // Parâmetros explícitos em vez de [AsParameters]: no .NET 10 o gerador de OpenAPI lança
    // InvalidCastException com atributos de validação em parâmetros posicionais de record
    private static async Task<IResult> ListTransfersAsync(
        IQueryHandler<ListTransfersQuery, PagedResponse<TransferResponse>> handler,
        CancellationToken cancellationToken,
        TransferStatus? status = null,
        Guid? accountId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var result = await handler.HandleAsync(
            new ListTransfersQuery(new TransferFilter(status, accountId, from, to, page, pageSize)),
            cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetTransferAsync(
        Guid id,
        HttpContext context,
        IAccountAccess access,
        IQueryHandler<GetTransferQuery, TransferResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTransferQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        // Pagador e recebedor acompanham a mesma transferência
        var transfer = result.Value;
        var canAccess =
            await access.CanAccessAsync(context.User, context.BearerToken(), transfer.SourceAccountId, cancellationToken)
            || await access.CanAccessAsync(context.User, context.BearerToken(), transfer.DestinationAccountId, cancellationToken);

        return canAccess
            ? TypedResults.Ok(transfer)
            : TypedResults.Problem(
                title: "Transfer.Forbidden",
                detail: "A transferência não envolve contas do usuário autenticado.",
                statusCode: StatusCodes.Status403Forbidden);
    }
}
