using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Domain.Analysis;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Fraud.Application.Analysis;

public sealed record DailyUsageResponse(Guid AccountId, DateOnly Date, decimal Used, decimal Limit, decimal Remaining);

/// <summary>Quanto do limite diário de transferências a conta já usou hoje.</summary>
public sealed record GetDailyUsageQuery(Guid AccountId) : IQuery<DailyUsageResponse>;

internal sealed class GetDailyUsageQueryHandler(
    FraudPolicy policy,
    IAccountActivityStore activity,
    TimeProvider clock) : IQueryHandler<GetDailyUsageQuery, DailyUsageResponse>
{
    public async Task<Result<DailyUsageResponse>> HandleAsync(GetDailyUsageQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var used = await activity.GetDailyUsageAsync(query.AccountId, now, cancellationToken);

        return new DailyUsageResponse(
            query.AccountId,
            policy.BusinessDate(now),
            used,
            policy.DailyLimit,
            Math.Max(0m, policy.DailyLimit - used));
    }
}
