using MassTransit;
using Semogly.Wallet.Contracts.Accounts;
using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.Notifications.Application.Contacts;
using Semogly.Wallet.Notifications.Application.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Notifications.Infrastructure.Messaging;

// Adaptadores de entrada: traduzem eventos em comandos da Application.
// A idempotência é garantida pelo Inbox do MassTransit e pela DeduplicationKey da notificação.

internal sealed class AccountCreatedConsumer(ICommandHandler<RegisterContactCommand> handler)
    : IConsumer<AccountCreated>
{
    public Task Consume(ConsumeContext<AccountCreated> context) =>
        handler.HandleOrThrowAsync(
            new RegisterContactCommand(context.Message.AccountId, context.Message.OwnerName, context.Message.Email),
            context.CancellationToken);
}

internal sealed class TransferCompletedConsumer(ICommandHandler<NotifyTransferCompletedCommand> handler)
    : IConsumer<TransferCompleted>
{
    public Task Consume(ConsumeContext<TransferCompleted> context) =>
        handler.HandleOrThrowAsync(
            new NotifyTransferCompletedCommand(
                context.Message.TransferId,
                context.Message.SourceAccountId,
                context.Message.DestinationAccountId,
                context.Message.Amount),
            context.CancellationToken);
}

internal sealed class TransferFailedConsumer(ICommandHandler<NotifyTransferFailedCommand> handler)
    : IConsumer<TransferFailed>
{
    public Task Consume(ConsumeContext<TransferFailed> context) =>
        handler.HandleOrThrowAsync(
            new NotifyTransferFailedCommand(
                context.Message.TransferId,
                context.Message.SourceAccountId,
                context.Message.Amount,
                context.Message.Reason),
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
