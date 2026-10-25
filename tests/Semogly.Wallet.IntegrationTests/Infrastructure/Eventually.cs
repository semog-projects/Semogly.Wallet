namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>Espera por consistência eventual: o fluxo entre serviços é assíncrono (mensageria).</summary>
internal static class Eventually
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public static async Task<T> GetAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + DefaultTimeout;
        T last;

        do
        {
            last = await probe();

            if (condition(last))
            {
                return last;
            }

            await Task.Delay(PollInterval, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Tempo esgotado aguardando: {description}. Último valor: {last}");
    }
}
