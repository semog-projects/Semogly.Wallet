using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.GetMyAccounts;

/// <summary>Contas do titular autenticado, identificado pelo e-mail do token.</summary>
public sealed record GetMyAccountsQuery(string? Email) : IQuery<IReadOnlyList<AccountResponse>>;

internal sealed class GetMyAccountsQueryHandler(IAccountReadService readService)
    : IQueryHandler<GetMyAccountsQuery, IReadOnlyList<AccountResponse>>
{
    public static readonly Error MissingEmail = Error.Validation(
        "Account.MissingEmail",
        "O token não identifica o titular: faça login com o e-mail cadastrado.");

    public async Task<Result<IReadOnlyList<AccountResponse>>> HandleAsync(
        GetMyAccountsQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Email))
        {
            return MissingEmail;
        }

        return Result.Success(
            await readService.GetByOwnerEmailAsync(query.Email.Trim().ToLowerInvariant(), cancellationToken));
    }
}
