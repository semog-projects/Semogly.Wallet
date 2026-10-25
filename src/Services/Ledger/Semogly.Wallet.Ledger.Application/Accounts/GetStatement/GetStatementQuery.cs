using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.GetStatement;

public enum EntryType
{
    Debit,
    Credit,
}

public sealed record StatementEntryResponse(
    Guid EntryId,
    EntryType Type,
    decimal Amount,
    decimal BalanceAfter,
    Guid? TransferId,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record StatementResponse(
    Guid AccountId,
    decimal Balance,
    int Page,
    int PageSize,
    IReadOnlyList<StatementEntryResponse> Entries);

/// <summary>Lado de leitura (CQRS): extrato a partir da projeção dos eventos.</summary>
public interface IStatementReadService
{
    Task<StatementResponse?> GetAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Extrato da conta, do lançamento mais recente para o mais antigo.</summary>
public sealed record GetStatementQuery(Guid AccountId, int Page, int PageSize) : IQuery<StatementResponse>;

internal sealed class GetStatementQueryHandler(IStatementReadService readService)
    : IQueryHandler<GetStatementQuery, StatementResponse>
{
    public async Task<Result<StatementResponse>> HandleAsync(GetStatementQuery query, CancellationToken cancellationToken)
    {
        var statement = await readService.GetAsync(query.AccountId, query.Page, query.PageSize, cancellationToken);

        return statement is null ? LedgerErrors.AccountNotFound(query.AccountId) : statement;
    }
}
