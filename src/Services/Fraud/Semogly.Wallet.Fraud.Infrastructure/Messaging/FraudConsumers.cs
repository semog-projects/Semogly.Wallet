using MassTransit;
using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.Fraud.Application.Analysis;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Fraud.Infrastructure.Messaging;

// Adaptadores de entrada: traduzem mensagens em comandos da Application.
// Sem Inbox (o serviço não tem banco): a idempotência vem do registro por TransferId no Redis.

internal sealed class CheckTransferFraudConsumer(ICommandHandler<AnalyzeTransferCommand> handler)
    : IConsumer<CheckTransferFraud>
{
    public Task Consume(ConsumeContext<CheckTransferFraud> context) =>
        handler.HandleOrThrowAsync(
            new AnalyzeTransferCommand(context.Message.TransferId, context.Message.SourceAccountId, context.Message.Amount),
            context.CancellationToken);
}

internal sealed class TransferFailedConsumer(ICommandHandler<ReleaseTransferCommand> handler)
    : IConsumer<TransferFailed>
{
    public Task Consume(ConsumeContext<TransferFailed> context) =>
        handler.HandleOrThrowAsync(
            new ReleaseTransferCommand(context.Message.TransferId, context.Message.SourceAccountId, context.Message.FailedAt),
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
