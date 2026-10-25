using Semogly.Wallet.Fraud.Api.ErrorHandling;
using Semogly.Wallet.Fraud.Application.Analysis;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Fraud.Api.Endpoints;

internal static class FraudEndpoints
{
    public static IEndpointRouteBuilder MapFraudEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/fraud/accounts/{accountId:guid}/daily-usage", GetDailyUsageAsync)
            .WithTags("Fraud")
            .WithName("GetDailyUsage")
            .WithSummary("Consulta quanto do limite diário de transferências a conta já usou hoje")
            .Produces<DailyUsageResponse>();

        return app;
    }

    private static async Task<IResult> GetDailyUsageAsync(
        Guid accountId,
        IQueryHandler<GetDailyUsageQuery, DailyUsageResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetDailyUsageQuery(accountId), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
