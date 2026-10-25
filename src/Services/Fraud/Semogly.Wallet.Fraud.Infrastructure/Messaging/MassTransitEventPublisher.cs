using MassTransit;
using Semogly.Wallet.Fraud.Application.Abstractions;

namespace Semogly.Wallet.Fraud.Infrastructure.Messaging;

/// <summary>Dentro de um consumer, publica pelo in-memory outbox: a mensagem só sai se o consumer concluir.</summary>
internal sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class =>
        publishEndpoint.Publish(integrationEvent, cancellationToken);
}
