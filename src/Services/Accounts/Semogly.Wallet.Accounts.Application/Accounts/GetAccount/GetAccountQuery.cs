using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.GetAccount;

public sealed record GetAccountQuery(Guid AccountId) : IQuery<AccountResponse>;

internal sealed class GetAccountQueryHandler(IAccountReadService readService)
    : IQueryHandler<GetAccountQuery, AccountResponse>
{
    public async Task<Result<AccountResponse>> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken)
    {
        var account = await readService.GetByIdAsync(query.AccountId, cancellationToken);

        return account is null ? AccountErrors.NotFound(query.AccountId) : account;
    }
}
