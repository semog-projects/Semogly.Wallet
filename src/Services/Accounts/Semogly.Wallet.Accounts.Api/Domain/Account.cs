namespace Semogly.Wallet.Accounts.Api.Domain;

/// <summary>
/// Conta da carteira digital. O saldo aqui é uma projeção dos lançamentos
/// efetivados pelo Ledger, que é a fonte da verdade financeira.
/// </summary>
public sealed class Account
{
    public const string DefaultCurrency = "BRL";
    private const int CpfLength = 11;

    // Construtor exigido pelo EF Core
    private Account() { }

    public Guid Id { get; private set; }
    public string OwnerName { get; private set; } = null!;
    public string Document { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Currency { get; private set; } = null!;
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Token de concorrência otimista (mapeado para a coluna de sistema xmin do Postgres).</summary>
    public uint Version { get; private set; }

    public static Account Open(string ownerName, string document, string email, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new Account
        {
            Id = Guid.CreateVersion7(now),
            OwnerName = ownerName.Trim(),
            Document = NormalizeDocument(document),
            Email = email.Trim().ToLowerInvariant(),
            Currency = DefaultCurrency,
            Balance = 0m,
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Mantém apenas os dígitos do CPF e valida o tamanho.</summary>
    public static string NormalizeDocument(string document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var digits = new string(document.Where(char.IsAsciiDigit).ToArray());

        return digits.Length == CpfLength
            ? digits
            : throw new DomainException($"O documento deve ser um CPF com {CpfLength} dígitos.");
    }

    public void ApplyDebit(decimal amount, DateTimeOffset now)
    {
        EnsurePositive(amount);
        Balance -= amount;
        UpdatedAt = now;
    }

    public void ApplyCredit(decimal amount, DateTimeOffset now)
    {
        EnsurePositive(amount);
        Balance += amount;
        UpdatedAt = now;
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("O valor do lançamento deve ser maior que zero.");
        }
    }
}
