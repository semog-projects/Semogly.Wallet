using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Notifications.Application.Contacts;

/// <summary>Guarda o contato da conta recém-aberta e envia o e-mail de boas-vindas.</summary>
public sealed record RegisterContactCommand(Guid AccountId, string Name, string Email) : ICommand;

internal sealed class RegisterContactCommandHandler(
    IContactRepository contacts,
    NotificationDispatcher dispatcher,
    TimeProvider clock) : ICommandHandler<RegisterContactCommand>
{
    public async Task<Result> HandleAsync(RegisterContactCommand command, CancellationToken cancellationToken)
    {
        var contact = await contacts.GetAsync(command.AccountId, cancellationToken);

        if (contact is null)
        {
            contact = Contact.Create(command.AccountId, command.Name, command.Email);
            contacts.Add(contact);
        }

        var welcome = Notification.Create(
            contact.AccountId,
            NotificationType.Welcome,
            contact.AccountId,
            contact.Email,
            NotificationContent.Welcome(contact),
            clock.GetUtcNow());

        await dispatcher.DispatchAsync([welcome], cancellationToken);

        return Result.Success();
    }
}
