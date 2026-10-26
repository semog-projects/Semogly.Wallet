using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.SearchAccounts;

/// <summary>Busca de contas para o backoffice; sem termo, lista as mais recentes.</summary>
public sealed record SearchAccountsQuery(string? Term, int Page, int PageSize) : IQuery<PagedResponse<AccountResponse>>;

internal sealed class SearchAccountsQueryHandler(IAccountReadService readService)
    : IQueryHandler<SearchAccountsQuery, PagedResponse<AccountResponse>>
{
    public async Task<Result<PagedResponse<AccountResponse>>> HandleAsync(
        SearchAccountsQuery query, CancellationToken cancellationToken) =>
        await readService.SearchAsync(query.Term?.Trim(), query.Page, query.PageSize, cancellationToken);
}
