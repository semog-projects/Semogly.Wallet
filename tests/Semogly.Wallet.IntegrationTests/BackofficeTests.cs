using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Consultas do backoffice: listagem de transferências, busca de contas e visão do livro.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class BackofficeTests(WalletFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WalletApi _api = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListTransfers_ShouldFilterByAccountAndStatus()
    {
        var ana = await _api.OpenAccountAsync("Ana Painel", initialDeposit: 100m);
        var bruno = await _api.OpenAccountAsync("Bruno Painel");

        var (_, completed) = await _api.RequestTransferAsync(ana, bruno, 40m);
        var (_, failed) = await _api.RequestTransferAsync(ana, bruno, 500m);
        await _api.WaitForOutcomeAsync(completed!.Id);
        await _api.WaitForOutcomeAsync(failed!.Id);

        // Conta casa com origem ou destino: Bruno aparece nas duas como destino
        var byAccount = await ListTransfersAsync($"accountId={bruno}");
        Assert.Equal(2, byAccount.TotalCount);
        Assert.Equal(failed.Id, byAccount.Items[0].Id); // mais recente primeiro

        var onlyFailed = await ListTransfersAsync($"accountId={ana}&status=Failed");
        var transfer = Assert.Single(onlyFailed.Items);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
    }

    [Fact]
    public async Task ListTransfers_ShouldPaginate()
    {
        var ana = await _api.OpenAccountAsync("Ana Paginada", initialDeposit: 100m);
        var bruno = await _api.OpenAccountAsync("Bruno Paginado");

        for (var i = 0; i < 3; i++)
        {
            await _api.RequestTransferAsync(ana, bruno, 1m);
        }

        var page = await ListTransfersAsync($"accountId={ana}&page=2&pageSize=2");

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task SearchAccounts_ShouldMatchNameEmailAndDocument()
    {
        var unique = $"Zuleica{Random.Shared.Next(100_000, 999_999)}";
        var accountId = await _api.OpenAccountAsync($"{unique} Teixeira");
        var account = (await fixture.Accounts.CreateClient()
            .GetFromJsonAsync<AccountResponse>($"/accounts/{accountId}", Json, Ct))!;

        Assert.Equal(accountId, Assert.Single((await SearchAsync(unique.ToUpperInvariant())).Items).Id);
        Assert.Equal(accountId, Assert.Single((await SearchAsync(account.Email)).Items).Id);
        Assert.Empty((await SearchAsync($"{unique}%")).Items); // % é literal, não curinga
    }

    [Fact]
    public async Task Book_ShouldAlwaysBeBalanced()
    {
        var ana = await _api.OpenAccountAsync("Ana Livro", initialDeposit: 250m);
        var bruno = await _api.OpenAccountAsync("Bruno Livro");
        var (_, transfer) = await _api.RequestTransferAsync(ana, bruno, 100m);
        await _api.WaitForOutcomeAsync(transfer!.Id);

        var book = (await fixture.Ledger.CreateClient().GetFromJsonAsync<LedgerBookResponse>("/ledger/book", Json, Ct))!;

        // Partidas dobradas: clientes + CashIn + Clearing = 0, mesmo com outros testes em andamento
        Assert.True(book.IsBalanced, $"Livro desbalanceado: {book}");
        Assert.True(book.CashIn <= -250m);
        Assert.Equal(-book.CashIn - book.Clearing, book.CustomerBalances);
    }

    private async Task<PagedResponse<TransferResponse>> ListTransfersAsync(string query) =>
        (await fixture.Transfers.CreateClient()
            .GetFromJsonAsync<PagedResponse<TransferResponse>>($"/transfers?{query}", Json, Ct))!;

    private async Task<PagedResponse<AccountResponse>> SearchAsync(string term) =>
        (await fixture.Accounts.CreateClient()
            .GetFromJsonAsync<PagedResponse<AccountResponse>>($"/accounts?search={Uri.EscapeDataString(term)}", Json, Ct))!;
}
