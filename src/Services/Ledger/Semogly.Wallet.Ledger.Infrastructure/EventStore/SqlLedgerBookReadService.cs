using Npgsql;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.Ledger.Domain.Accounts;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>
/// Saldo atual de cada conta = saldo após o lançamento mais recente na projeção do extrato.
/// SQL direto (DISTINCT ON) para agregar no banco em vez de carregar todos os lançamentos.
/// </summary>
internal sealed class SqlLedgerBookReadService(NpgsqlDataSource dataSource) : ILedgerBookReadService
{
    // Documentos do Marten ficam em jsonb, com os nomes das propriedades em PascalCase
    private static readonly string LatestBalancesSql = $"""
        select (data->>'AccountId')::uuid, (data->>'BalanceAfter')::numeric
        from (
            select distinct on (data->>'AccountId') data
            from {DependencyInjection.EventStoreSchema}.mt_doc_statemententry
            order by data->>'AccountId', (data->>'Sequence')::bigint desc
        ) latest
        """;

    public async Task<LedgerBookResponse> GetAsync(CancellationToken cancellationToken)
    {
        decimal cashIn = 0m, clearing = 0m, customers = 0m;
        var customerAccounts = 0;

        await using var command = dataSource.CreateCommand(LatestBalancesSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var accountId = reader.GetGuid(0);
            var balance = reader.GetDecimal(1);

            if (accountId == SystemAccounts.CashIn)
            {
                cashIn = balance;
            }
            else if (accountId == SystemAccounts.Clearing)
            {
                clearing = balance;
            }
            else
            {
                customers += balance;
                customerAccounts++;
            }
        }

        return new LedgerBookResponse(cashIn, clearing, customers, customerAccounts, cashIn + clearing + customers);
    }
}
