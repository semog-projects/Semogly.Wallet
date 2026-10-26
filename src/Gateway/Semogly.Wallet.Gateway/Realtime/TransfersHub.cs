using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Semogly.Wallet.Gateway.Security;

namespace Semogly.Wallet.Gateway.Realtime;

/// <summary>Atualização de status de uma transferência enviada aos clientes conectados.</summary>
public sealed record TransferUpdate(
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Status,
    string? Reason,
    DateTimeOffset OccurredAt);

/// <summary>Métodos que o servidor invoca nos clientes (tipado para evitar strings mágicas).</summary>
public interface ITransfersClient
{
    Task TransferUpdated(TransferUpdate update);
}

/// <summary>
/// Hub de transferências em tempo real. O cliente assina as contas que quer acompanhar;
/// operadores entram automaticamente no grupo do backoffice e recebem todas as transferências.
/// </summary>
[Authorize]
public sealed class TransfersHub : Hub<ITransfersClient>
{
    public const string Route = "/hubs/transfers";
    public const string BackofficeGroup = "backoffice";

    public static string AccountGroup(Guid accountId) => $"account:{accountId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(GatewayPolicies.OperatorRole) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BackofficeGroup);
        }

        await base.OnConnectedAsync();
    }

    // Pendente: verificar se a conta pertence ao usuário do token (mesma limitação das rotas REST)
    public Task SubscribeToAccount(Guid accountId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));

    public Task UnsubscribeFromAccount(Guid accountId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AccountGroup(accountId));
}
