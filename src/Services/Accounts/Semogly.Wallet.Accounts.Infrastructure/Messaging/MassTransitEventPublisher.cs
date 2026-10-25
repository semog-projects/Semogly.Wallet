using MassTransit;
using Semogly.Wallet.Accounts.Application.Abstractions;

namespace Semogly.Wallet.Accounts.Infrastructure.Messaging;

/// <summary>Publica via Bus Outbox: a mensagem é gravada no banco e enviada após o commit.</summary>
internal sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class =>
        publishEndpoint.Publish(integrationEvent, cancellationToken);
}
