using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Semogly.Wallet.Gateway.Realtime;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Eventos reais da saga chegando aos clientes pelo hub SignalR do Gateway.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class RealtimeTests(WalletFixture fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly WalletApi _api = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AccountSubscriber_ShouldReceivePendingAndCompleted()
    {
        var ana = await _api.OpenAccountAsync("Ana Tempo Real", initialDeposit: 100m);
        var bruno = await _api.OpenAccountAsync("Bruno Tempo Real");

        await using var client = await ConnectAsync("bruno.realtime@email.com", "customer");
        await client.Connection.InvokeAsync(nameof(TransfersHub.SubscribeToAccount), bruno, Ct);

        var (_, transfer) = await _api.RequestTransferAsync(ana, bruno, 25m);

        var pending = await client.NextAsync(u => u.TransferId == transfer!.Id);
        Assert.Equal("Pending", pending.Status);

        var completed = await client.NextAsync(u => u.TransferId == transfer!.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(25m, completed.Amount);
    }

    [Fact]
    public async Task Operator_ShouldReceiveEveryTransferIncludingFailures()
    {
        var ana = await _api.OpenAccountAsync("Ana Backoffice");
        var bruno = await _api.OpenAccountAsync("Bruno Backoffice");

        // Sem assinar conta nenhuma: o papel operator entra no grupo do backoffice
        await using var backoffice = await ConnectAsync("operador.realtime@semogly.dev", "operator");

        var (_, transfer) = await _api.RequestTransferAsync(ana, bruno, 10m);

        var failed = await backoffice.NextAsync(u => u.TransferId == transfer!.Id && u.Status == "Failed");
        Assert.Equal("Saldo insuficiente.", failed.Reason);
    }

    [Fact]
    public async Task Connection_WithoutToken_ShouldBeRejected()
    {
        var connection = BuildConnection(accessToken: null);

        var error = await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync(Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private async Task<HubClient> ConnectAsync(string username, string role)
    {
        await fixture.Keycloak.CreateAsync(username, username, role);
        var token = await fixture.Keycloak.TokenForAsync(username);

        var connection = BuildConnection(token);
        var updates = Channel.CreateUnbounded<TransferUpdate>();
        connection.On<TransferUpdate>(nameof(ITransfersClient.TransferUpdated), u => updates.Writer.TryWrite(u));

        await connection.StartAsync(Ct);
        return new HubClient(connection, updates.Reader);
    }

    private HubConnection BuildConnection(string? accessToken)
    {
        var server = fixture.Gateway.Server;

        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, TransfersHub.Route.TrimStart('/')), options =>
            {
                // TestServer não tem WebSocket real: long polling passa pelo handler em memória
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();
    }

    private sealed class HubClient(HubConnection connection, ChannelReader<TransferUpdate> updates) : IAsyncDisposable
    {
        public HubConnection Connection => connection;

        public async Task<TransferUpdate> NextAsync(Func<TransferUpdate, bool> match)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(Timeout);

            await foreach (var update in updates.ReadAllAsync(timeout.Token))
            {
                if (match(update))
                {
                    return update;
                }
            }

            throw new TimeoutException("Nenhuma atualização correspondente recebida pelo hub.");
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
