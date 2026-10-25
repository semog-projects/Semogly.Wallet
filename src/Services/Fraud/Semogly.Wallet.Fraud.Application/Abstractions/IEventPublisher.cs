namespace Semogly.Wallet.Fraud.Application.Abstractions;

/// <summary>
/// Publica eventos de integração. A implementação usa o in-memory outbox:
/// o evento só é enviado ao broker após o consumer concluir com sucesso.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;
}
