using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Transfers.Domain.Transfers;

/// <summary>
/// Transferência entre contas. Registra o pedido e o desfecho; a orquestração
/// dos passos (antifraude, débito, crédito e estorno) fica a cargo da saga.
/// </summary>
public sealed class Transfer
{
    public const int IdempotencyKeyMaxLength = 100;

    // Construtor exigido pelo EF Core
    private Transfer() { }

    public Guid Id { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public TransferStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Token de concorrência otimista (mapeado para a coluna de sistema xmin do Postgres).</summary>
    public uint Version { get; private set; }

    public static Transfer Request(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string idempotencyKey,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (sourceAccountId == destinationAccountId)
        {
            throw new DomainException("A conta de origem deve ser diferente da conta de destino.");
        }

        if (amount <= 0)
        {
            throw new DomainException("O valor da transferência deve ser maior que zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("O valor da transferência deve ter no máximo duas casas decimais.");
        }

        return new Transfer
        {
            Id = Guid.CreateVersion7(now),
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            Amount = amount,
            IdempotencyKey = idempotencyKey,
            Status = TransferStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Indica se um novo pedido com a mesma chave de idempotência tem os mesmos dados.</summary>
    public bool IsSameRequest(Guid sourceAccountId, Guid destinationAccountId, decimal amount) =>
        SourceAccountId == sourceAccountId && DestinationAccountId == destinationAccountId && Amount == amount;

    public void Complete(DateTimeOffset now)
    {
        EnsurePending();
        Status = TransferStatus.Completed;
        UpdatedAt = now;
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsurePending();
        Status = TransferStatus.Failed;
        FailureReason = reason;
        UpdatedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != TransferStatus.Pending)
        {
            throw new DomainException($"A transferência {Id} já foi finalizada com status {Status}.");
        }
    }
}
