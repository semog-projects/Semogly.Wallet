using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Semogly.Wallet.Contracts.Transfers;

namespace Semogly.Wallet.Gateway.Realtime;

// Repassam os eventos da saga aos clientes SignalR das contas envolvidas e ao backoffice.
// Com várias instâncias do Gateway, só uma consome cada evento; o backplane Redis entrega a todas.

internal sealed class TransferRequestedConsumer(IHubContext<TransfersHub, ITransfersClient> hub)
    : IConsumer<TransferRequested>
{
    public Task Consume(ConsumeContext<TransferRequested> context) =>
        hub.BroadcastAsync(new TransferUpdate(
            context.Message.TransferId,
            context.Message.SourceAccountId,
            context.Message.DestinationAccountId,
            context.Message.Amount,
            TransferStatuses.Pending,
            null,
            context.Message.RequestedAt));
}

internal sealed class TransferCompletedConsumer(IHubContext<TransfersHub, ITransfersClient> hub)
    : IConsumer<TransferCompleted>
{
    public Task Consume(ConsumeContext<TransferCompleted> context) =>
        hub.BroadcastAsync(new TransferUpdate(
            context.Message.TransferId,
            context.Message.SourceAccountId,
            context.Message.DestinationAccountId,
            context.Message.Amount,
            TransferStatuses.Completed,
            null,
            context.Message.CompletedAt));
}

internal sealed class TransferFailedConsumer(IHubContext<TransfersHub, ITransfersClient> hub)
    : IConsumer<TransferFailed>
{
    public Task Consume(ConsumeContext<TransferFailed> context) =>
        hub.BroadcastAsync(new TransferUpdate(
            context.Message.TransferId,
            context.Message.SourceAccountId,
            context.Message.DestinationAccountId,
            context.Message.Amount,
            TransferStatuses.Failed,
            context.Message.Reason,
            context.Message.FailedAt));
}

/// <summary>Mesmos nomes do enum TransferStatus do serviço Transfers.</summary>
internal static class TransferStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

file static class HubContextExtensions
{
    public static Task BroadcastAsync(this IHubContext<TransfersHub, ITransfersClient> hub, TransferUpdate update) =>
        hub.Clients
            .Groups(
                TransfersHub.AccountGroup(update.SourceAccountId),
                TransfersHub.AccountGroup(update.DestinationAccountId),
                TransfersHub.BackofficeGroup)
            .TransferUpdated(update);
}
