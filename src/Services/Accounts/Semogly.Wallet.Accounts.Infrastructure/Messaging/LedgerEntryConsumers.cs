using MassTransit;
using Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Accounts.Infrastructure.Messaging;

// Adaptadores de entrada: traduzem eventos do Ledger em comandos da Application.
// A idempotência é garantida pelo Inbox do MassTransit (deduplicação por MessageId).

internal sealed class AccountDebitedConsumer(ICommandHandler<ApplyLedgerEntryCommand> handler)
    : IConsumer<AccountDebited>
{
    public Task Consume(ConsumeContext<AccountDebited> context) =>
        handler.HandleOrThrowAsync(
            new ApplyLedgerEntryCommand(context.Message.AccountId, context.Message.Amount, LedgerEntryType.Debit),
            context.CancellationToken);
}

internal sealed class AccountCreditedConsumer(ICommandHandler<ApplyLedgerEntryCommand> handler)
    : IConsumer<AccountCredited>
{
    public Task Consume(ConsumeContext<AccountCredited> context) =>
        handler.HandleOrThrowAsync(
            new ApplyLedgerEntryCommand(context.Message.AccountId, context.Message.Amount, LedgerEntryType.Credit),
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
