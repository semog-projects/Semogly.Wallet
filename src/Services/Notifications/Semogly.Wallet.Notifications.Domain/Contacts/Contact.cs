namespace Semogly.Wallet.Notifications.Domain.Contacts;

/// <summary>
/// Cópia local dos dados de contato do titular, alimentada pelo evento AccountCreated.
/// Evita consultar o serviço Accounts a cada notificação.
/// </summary>
public sealed class Contact
{
    // Construtor exigido pelo EF Core
    private Contact() { }

    public Guid AccountId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    public static Contact Create(Guid accountId, string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new Contact { AccountId = accountId, Name = name, Email = email };
    }

    /// <summary>Primeiro nome, para uma saudação mais pessoal.</summary>
    public string FirstName => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
}
