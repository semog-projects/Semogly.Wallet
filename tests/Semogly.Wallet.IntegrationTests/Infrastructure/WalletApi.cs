using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>Cliente HTTP dos serviços hospedados pelo <see cref="WalletFixture"/>.</summary>
internal sealed class WalletApi(WalletFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _accounts = fixture.Accounts.CreateClient();
    private readonly HttpClient _transfers = fixture.Transfers.CreateClient();
    private readonly HttpClient _ledger = fixture.Ledger.CreateClient();
    private readonly HttpClient _notifications = fixture.Notifications.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Abre a conta e espera o Ledger registrá-la (evento AccountCreated).</summary>
    public async Task<Guid> OpenAccountAsync(string name, decimal initialDeposit = 0m)
    {
        var response = await _accounts.PostAsJsonAsync(
            "/accounts",
            new { ownerName = name, document = RandomCpf(), email = $"{name.ToLowerInvariant().Replace(' ', '.')}@email.com" },
            Ct);
        response.EnsureSuccessStatusCode();
        var account = (await response.Content.ReadFromJsonAsync<AccountResponse>(Json, Ct))!;

        // O extrato retorna 404 até o Ledger consumir o AccountCreated
        await Eventually.GetAsync(
            async () => (await _ledger.GetAsync($"/ledger/accounts/{account.Id}/statement", Ct)).StatusCode,
            status => status == HttpStatusCode.OK,
            $"conta {account.Id} aberta no Ledger");

        if (initialDeposit > 0)
        {
            Assert.Equal(HttpStatusCode.OK, await DepositAsync(account.Id, initialDeposit));
        }

        return account.Id;
    }

    public async Task<HttpStatusCode> DepositAsync(Guid accountId, decimal amount)
    {
        var response = await _ledger.PostAsJsonAsync($"/ledger/accounts/{accountId}/deposits", new { amount }, Ct);
        return response.StatusCode;
    }

    public async Task<(HttpStatusCode Status, TransferResponse? Transfer)> RequestTransferAsync(
        Guid source, Guid destination, decimal amount, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new { sourceAccountId = source, destinationAccountId = destination, amount }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        var response = await _transfers.SendAsync(request, Ct);
        var transfer = response.StatusCode == HttpStatusCode.Accepted
            ? await response.Content.ReadFromJsonAsync<TransferResponse>(Json, Ct)
            : null;

        return (response.StatusCode, transfer);
    }

    /// <summary>Espera a saga terminar (status diferente de Pending).</summary>
    public Task<TransferResponse> WaitForOutcomeAsync(Guid transferId) =>
        Eventually.GetAsync(
            async () => (await _transfers.GetFromJsonAsync<TransferResponse>($"/transfers/{transferId}", Json, Ct))!,
            transfer => transfer.Status != TransferStatus.Pending,
            $"desfecho da transferência {transferId}");

    public async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        using var document = await JsonDocument.ParseAsync(
            await _accounts.GetStreamAsync($"/accounts/{accountId}/balance", Ct), cancellationToken: Ct);
        return document.RootElement.GetProperty("balance").GetDecimal();
    }

    public async Task<decimal> GetLedgerBalanceAsync(Guid accountId)
    {
        using var document = await JsonDocument.ParseAsync(
            await _ledger.GetStreamAsync($"/ledger/accounts/{accountId}/statement?pageSize=1", Ct), cancellationToken: Ct);
        return document.RootElement.GetProperty("balance").GetDecimal();
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(Guid accountId) =>
        (await _notifications.GetFromJsonAsync<List<NotificationResponse>>($"/notifications/accounts/{accountId}", Json, Ct))!;

    private static string RandomCpf() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString("D11", null);
}
