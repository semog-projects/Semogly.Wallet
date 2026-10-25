namespace Semogly.Wallet.Transfers.Application.Abstractions;

/// <summary>
/// Lançada pelo <see cref="IUnitOfWork"/> quando uma restrição de unicidade do banco é violada,
/// sem expor exceções do provedor de dados para a Application.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException() { }

    public UniqueConstraintViolationException(string message) : base(message) { }

    public UniqueConstraintViolationException(string message, Exception innerException) : base(message, innerException) { }
}
