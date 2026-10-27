using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.Transfers.UnitTests.Sagas;

/// <summary>Saga no test harness em memória, com agendador de mensagens em memória para os timeouts.</summary>
internal static class SagaTestHost
{
    /// <param name="inactivityTimeout">
    /// Com timeouts curtos, a saga fica alguns segundos sem mensagens à espera do prazo: o harness não pode
    /// considerar o teste inativo antes disso.
    /// </param>
    public static ServiceProvider Build(TransferSagaOptions options, TimeSpan? inactivityTimeout = null) => new ServiceCollection()
        .AddSingleton(TimeProvider.System)
        .AddSingleton(options)
        .AddMassTransitTestHarness(bus =>
        {
            if (inactivityTimeout is { } inactivity)
            {
                bus.SetTestTimeouts(testInactivityTimeout: inactivity);
            }

            bus.AddDelayedMessageScheduler();
            bus.AddSagaStateMachine<TransferStateMachine, TransferState>();
            bus.UsingInMemory((context, memory) =>
            {
                memory.UseDelayedMessageScheduler();
                memory.ConfigureEndpoints(context);
            });
        })
        .BuildServiceProvider(validateScopes: true);
}
