namespace Semogly.Wallet.Notifications.Domain.Contacts;

public interface IContactRepository
{
    Task<Contact?> GetAsync(Guid accountId, CancellationToken cancellationToken);

    void Add(Contact contact);
}
