using MassTransit;

namespace Semogly.Wallet.Transfers.Infrastructure.Sagas;

/// <summary>Estado persistido da saga de transferência (uma instância por transferência em andamento).</summary>
public sealed class TransferState : SagaStateMachineInstance
{
    /// <summary>Igual ao Id da transferência.</summary>
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = null!;
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Motivo da falha do crédito, guardado enquanto o estorno está em andamento.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Concorrência otimista (xmin): evita que duas mensagens avancem a mesma saga ao mesmo tempo.</summary>
    public uint RowVersion { get; set; }
}
