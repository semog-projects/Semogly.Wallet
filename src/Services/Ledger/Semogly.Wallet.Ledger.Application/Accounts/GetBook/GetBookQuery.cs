using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.GetBook;

/// <summary>
/// Fotografia do livro-razão: como todo lançamento tem contrapartida, a soma de todas as contas
/// (clientes + CashIn + Clearing) precisa ser zero. Qualquer outro valor indica inconsistência.
/// </summary>
public sealed record LedgerBookResponse(
    decimal CashIn,
    decimal Clearing,
    decimal CustomerBalances,
    int CustomerAccounts,
    decimal Total)
{
    public bool IsBalanced => Total == 0m;
}

/// <summary>Lado de leitura (CQRS): saldos atuais a partir da projeção do extrato.</summary>
public interface ILedgerBookReadService
{
    Task<LedgerBookResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed record GetBookQuery : IQuery<LedgerBookResponse>;

internal sealed class GetBookQueryHandler(ILedgerBookReadService readService) : IQueryHandler<GetBookQuery, LedgerBookResponse>
{
    public async Task<Result<LedgerBookResponse>> HandleAsync(GetBookQuery query, CancellationToken cancellationToken) =>
        await readService.GetAsync(cancellationToken);
}
