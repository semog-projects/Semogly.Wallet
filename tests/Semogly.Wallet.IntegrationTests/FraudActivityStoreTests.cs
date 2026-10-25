using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Domain.Analysis;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Script Lua do antifraude contra um Redis real (limite diário de 10.000 e 5 por minuto).</summary>
[Collection(WalletTestGroup.Name)]
public sealed class FraudActivityStoreTests(WalletFixture fixture)
{
    private readonly IAccountActivityStore _store = fixture.Fraud.Services.GetRequiredService<IAccountActivityStore>();
    private readonly Guid _account = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<FraudRejection?> RegisterAsync(decimal amount, Guid? transferId = null) =>
        _store.TryRegisterAsync(_account, transferId ?? Guid.NewGuid(), amount, _now, Ct);

    [Fact]
    public async Task DailyLimit_ShouldAllowUpToTheLimitAndRejectBeyond()
    {
        Assert.Null(await RegisterAsync(6_000m));
        Assert.Null(await RegisterAsync(4_000m));

        Assert.Equal(FraudRejection.DailyLimitExceeded, await RegisterAsync(0.01m));
        Assert.Equal(10_000m, await _store.GetDailyUsageAsync(_account, _now, Ct));
    }

    [Fact]
    public async Task Velocity_ShouldRejectTheSixthTransferInTheWindow()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await RegisterAsync(1m));
        }

        Assert.Equal(FraudRejection.VelocityExceeded, await RegisterAsync(1m));
    }

    [Fact]
    public async Task SameTransfer_RegisteredTwice_ShouldCountOnce()
    {
        var transferId = Guid.NewGuid();

        Assert.Null(await RegisterAsync(700m, transferId));
        Assert.Null(await RegisterAsync(700m, transferId));

        Assert.Equal(700m, await _store.GetDailyUsageAsync(_account, _now, Ct));
    }

    [Fact]
    public async Task Release_ShouldGiveTheAmountBackToTheDailyLimit()
    {
        var transferId = Guid.NewGuid();
        await RegisterAsync(2_500m, transferId);

        await _store.ReleaseAsync(_account, transferId, _now, Ct);

        Assert.Equal(0m, await _store.GetDailyUsageAsync(_account, _now, Ct));
    }

    [Fact]
    public async Task ConcurrentAnalyses_ShouldNeverExceedTheDailyLimit()
    {
        // 20 análises simultâneas de 3.000: só 3 cabem no limite de 10.000
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => RegisterAsync(3_000m)));

        Assert.Equal(3, results.Count(r => r is null));
        Assert.Equal(9_000m, await _store.GetDailyUsageAsync(_account, _now, Ct));
    }
}
