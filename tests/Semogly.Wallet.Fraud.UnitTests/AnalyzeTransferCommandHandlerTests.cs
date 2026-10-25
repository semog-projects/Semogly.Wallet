using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Application.Analysis;
using Semogly.Wallet.Fraud.Domain.Analysis;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Fraud.UnitTests;

public class AnalyzeTransferCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FraudPolicy _policy = new() { MaxTransactionAmount = 1_000m };
    private readonly FakeActivityStore _activity = new();
    private readonly FakeEventPublisher _publisher = new();

    private AnalyzeTransferCommandHandler CreateHandler() =>
        new(_policy, _activity, _publisher, new FixedTimeProvider(Now));

    private Task<Result> AnalyzeAsync(decimal amount) =>
        CreateHandler().HandleAsync(
            new AnalyzeTransferCommand(Guid.NewGuid(), Guid.NewGuid(), amount), TestContext.Current.CancellationToken);

    [Fact]
    public async Task WithinAllLimits_ShouldApproveAndRegisterActivity()
    {
        await AnalyzeAsync(500m);

        Assert.IsType<TransferFraudCheckApproved>(Assert.Single(_publisher.Published));
        Assert.Equal(1, _activity.RegisterCalls);
    }

    [Fact]
    public async Task AboveTransactionLimit_ShouldRejectWithoutConsumingDailyLimit()
    {
        await AnalyzeAsync(1_000.01m);

        var rejected = Assert.IsType<TransferFraudCheckRejected>(Assert.Single(_publisher.Published));
        Assert.Equal(FraudRejection.TransactionLimitExceeded.ToReason(), rejected.Reason);
        Assert.Equal(0, _activity.RegisterCalls);
    }

    [Theory]
    [InlineData(FraudRejection.DailyLimitExceeded)]
    [InlineData(FraudRejection.VelocityExceeded)]
    public async Task WhenActivityStoreRejects_ShouldPublishRejectionWithReason(FraudRejection rejection)
    {
        _activity.NextResult = rejection;

        await AnalyzeAsync(100m);

        var rejected = Assert.IsType<TransferFraudCheckRejected>(Assert.Single(_publisher.Published));
        Assert.Equal(rejection.ToReason(), rejected.Reason);
    }

    [Theory]
    [InlineData(1_000, false)]
    [InlineData(1_000.01, true)]
    public void TransactionLimitRule_ShouldAllowUpToTheLimit(decimal amount, bool rejected) =>
        Assert.Equal(rejected, TransactionLimitRule.Evaluate(amount, _policy) is not null);

    [Fact]
    public void BusinessDate_ShouldUseBrasiliaTimeZone()
    {
        // 01:00 UTC ainda é o dia anterior em Brasília (UTC-3)
        var earlyUtc = new DateTimeOffset(2026, 10, 26, 1, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 10, 25), new FraudPolicy().BusinessDate(earlyUtc));
    }

    private sealed class FakeActivityStore : IAccountActivityStore
    {
        public FraudRejection? NextResult { get; set; }
        public int RegisterCalls { get; private set; }

        public Task<FraudRejection?> TryRegisterAsync(
            Guid accountId, Guid transferId, decimal amount, DateTimeOffset now, CancellationToken cancellationToken)
        {
            RegisterCalls++;
            return Task.FromResult(NextResult);
        }

        public Task ReleaseAsync(Guid accountId, Guid transferId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<decimal> GetDailyUsageAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(0m);
    }

    private sealed class FakeEventPublisher : IEventPublisher
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : class
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
