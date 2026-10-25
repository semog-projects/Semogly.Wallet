using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Semogly.Wallet.Transfers.Infrastructure.Sagas;

internal sealed class TransferStateMap : SagaClassMap<TransferState>
{
    protected override void Configure(EntityTypeBuilder<TransferState> entity, ModelBuilder model)
    {
        entity.ToTable("transfer_sagas");
        entity.Property(s => s.CurrentState).HasMaxLength(64);
        entity.Property(s => s.Amount).HasPrecision(18, 2);
        entity.Property(s => s.FailureReason).HasMaxLength(500);
        entity.Property(s => s.RowVersion).IsRowVersion();
    }
}
