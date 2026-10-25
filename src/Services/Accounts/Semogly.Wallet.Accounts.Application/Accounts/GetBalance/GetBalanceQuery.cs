using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.GetBalance;

public sealed record BalanceResponse(Guid AccountId, decimal Balance, string Currency);

public sealed record GetBalanceQuery(Guid AccountId) : IQuery<BalanceResponse>;

internal sealed class GetBalanceQueryHandler(IAccountReadService readService)
    : IQueryHandler<GetBalanceQuery, BalanceResponse>
{
    public async Task<Result<BalanceResponse>> HandleAsync(GetBalanceQuery query, CancellationToken cancellationToken)
    {
        var account = await readService.GetByIdAsync(query.AccountId, cancellationToken);

        return account is null
            ? AccountErrors.NotFound(query.AccountId)
            : new BalanceResponse(account.Id, account.Balance, account.Currency);
    }
}
