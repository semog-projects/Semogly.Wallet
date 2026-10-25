using MassTransit;
using Semogly.Wallet.Contracts.Accounts;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Accounts.OpenAccount;
using Semogly.Wallet.Ledger.Application.Accounts.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Ledger.Infrastructure.Messaging;

// Adaptadores de entrada: traduzem mensagens em comandos da Application.
// A idempotência é garantida pelo Inbox do MassTransit (deduplicação por MessageId).

internal sealed class AccountCreatedConsumer(ICommandHandler<OpenLedgerAccountCommand> handler)
    : IConsumer<AccountCreated>
{
    public Task Consume(ConsumeContext<AccountCreated> context) =>
        handler.HandleOrThrowAsync(new OpenLedgerAccountCommand(context.Message.AccountId), context.CancellationToken);
}

internal sealed class DebitAccountConsumer(ICommandHandler<DebitForTransferCommand> handler)
    : IConsumer<DebitAccount>
{
    public Task Consume(ConsumeContext<DebitAccount> context) =>
        handler.HandleOrThrowAsync(
            new DebitForTransferCommand(context.Message.TransferId, context.Message.AccountId, context.Message.Amount),
            context.CancellationToken);
}

internal sealed class CreditAccountConsumer(ICommandHandler<CreditForTransferCommand> handler)
    : IConsumer<CreditAccount>
{
    public Task Consume(ConsumeContext<CreditAccount> context) =>
        handler.HandleOrThrowAsync(
            new CreditForTransferCommand(context.Message.TransferId, context.Message.AccountId, context.Message.Amount),
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
