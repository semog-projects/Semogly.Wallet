using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Notifications.Application.Transfers;

public sealed record NotifyTransferCompletedCommand(
    Guid TransferId, Guid SourceAccountId, Guid DestinationAccountId, decimal Amount) : ICommand;

public sealed record NotifyTransferFailedCommand(
    Guid TransferId, Guid SourceAccountId, decimal Amount, string Reason) : ICommand;

/// <summary>Avisa quem enviou e quem recebeu.</summary>
internal sealed class NotifyTransferCompletedCommandHandler(
    IContactRepository contacts,
    NotificationDispatcher dispatcher,
    TimeProvider clock) : ICommandHandler<NotifyTransferCompletedCommand>
{
    public async Task<Result> HandleAsync(NotifyTransferCompletedCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var source = await contacts.GetAsync(command.SourceAccountId, cancellationToken);
        var destination = await contacts.GetAsync(command.DestinationAccountId, cancellationToken);

        // Contas abertas antes deste serviço existir não têm contato registrado: são ignoradas
        List<Notification> pending = [];

        if (source is not null)
        {
            pending.Add(Notification.Create(
                source.AccountId, NotificationType.TransferSent, command.TransferId, source.Email,
                NotificationContent.TransferSent(source, command.Amount), now));
        }

        if (destination is not null)
        {
            pending.Add(Notification.Create(
                destination.AccountId, NotificationType.TransferReceived, command.TransferId, destination.Email,
                NotificationContent.TransferReceived(destination, command.Amount), now));
        }

        await dispatcher.DispatchAsync(pending, cancellationToken);

        return Result.Success();
    }
}

/// <summary>Avisa quem enviou que a transferência não foi realizada, com o motivo.</summary>
internal sealed class NotifyTransferFailedCommandHandler(
    IContactRepository contacts,
    NotificationDispatcher dispatcher,
    TimeProvider clock) : ICommandHandler<NotifyTransferFailedCommand>
{
    public async Task<Result> HandleAsync(NotifyTransferFailedCommand command, CancellationToken cancellationToken)
    {
        var source = await contacts.GetAsync(command.SourceAccountId, cancellationToken);

        if (source is null)
        {
            return Result.Success();
        }

        var notification = Notification.Create(
            source.AccountId, NotificationType.TransferFailed, command.TransferId, source.Email,
            NotificationContent.TransferFailed(source, command.Amount, command.Reason), clock.GetUtcNow());

        await dispatcher.DispatchAsync([notification], cancellationToken);

        return Result.Success();
    }
}
