namespace Semogly.Wallet.Ledger.Application.Abstractions;

/// <summary>
/// Publica eventos de integração. A implementação usa o Transactional Outbox:
/// o evento só é enviado ao broker após o <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;
}
