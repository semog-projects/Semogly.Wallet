using MassTransit;
using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.Transfers.Application.Transfers.CompleteTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.FailTransfer;

namespace Semogly.Wallet.Transfers.Infrastructure.Messaging;

// Adaptadores de entrada: refletem no agregado Transfer o desfecho publicado pela saga.
// A idempotência é garantida pelo Inbox do MassTransit (deduplicação por MessageId).

internal sealed class TransferCompletedConsumer(ICommandHandler<CompleteTransferCommand> handler)
    : IConsumer<TransferCompleted>
{
    public Task Consume(ConsumeContext<TransferCompleted> context) =>
        handler.HandleOrThrowAsync(new CompleteTransferCommand(context.Message.TransferId), context.CancellationToken);
}

internal sealed class TransferFailedConsumer(ICommandHandler<FailTransferCommand> handler)
    : IConsumer<TransferFailed>
{
    public Task Consume(ConsumeContext<TransferFailed> context) =>
        handler.HandleOrThrowAsync(
            new FailTransferCommand(context.Message.TransferId, context.Message.Reason),
            context.CancellationToken);
}

file static class CommandHandlerExtensions
{
    /// <summary>Falhas viram exceção para acionar o retry e, esgotado, a fila de erro.</summary>
    public static async Task HandleOrThrowAsync<TCommand>(
        this ICommandHandler<TCommand> handler, TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
        }
    }
}
