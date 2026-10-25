using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.Transfers.Application.Abstractions;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;
using Semogly.Wallet.Transfers.Domain.Transfers;
using Semogly.Wallet.Transfers.UnitTests.Fakes;

namespace Semogly.Wallet.Transfers.UnitTests.Application;

public class CreateTransferCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();

    private readonly FakeTransferRepository _repository = new();
    private readonly FakeEventPublisher _publisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateTransferCommandHandler CreateHandler() =>
        new(_repository, _publisher, _unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task HandleAsync_WithNewKey_ShouldCreateTransferAndStartSaga()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateTransferCommand(Source, Destination, 100m, "key-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var transfer = Assert.Single(_repository.Transfers);
        Assert.Equal(TransferStatus.Pending, result.Value.Status);
        var published = Assert.IsType<TransferRequested>(Assert.Single(_publisher.Published));
        Assert.Equal(transfer.Id, published.TransferId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_WithRepeatedKeyAndSameData_ShouldReturnOriginalTransfer()
    {
        var original = Transfer.Request(Source, Destination, 100m, "key-1", Now);
        _repository.Add(original);

        var result = await CreateHandler().HandleAsync(
            new CreateTransferCommand(Source, Destination, 100m, "key-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(original.Id, result.Value.Id);
        Assert.Empty(_publisher.Published);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_WithRepeatedKeyAndDifferentData_ShouldReturnConflict()
    {
        _repository.Add(Transfer.Request(Source, Destination, 100m, "key-1", Now));

        var result = await CreateHandler().HandleAsync(
            new CreateTransferCommand(Source, Destination, 999m, "key-1"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrors.IdempotencyKeyReused, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithoutKey_ShouldReturnValidationError(string key)
    {
        var result = await CreateHandler().HandleAsync(
            new CreateTransferCommand(Source, Destination, 100m, key), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrors.InvalidIdempotencyKey, result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenConcurrentRequestWins_ShouldReturnWinningTransfer()
    {
        // Simula outra requisição com a mesma chave gravando entre a checagem e o commit
        var winner = Transfer.Request(Source, Destination, 100m, "key-1", Now);
        _unitOfWork.OnSave = () =>
        {
            _repository.Transfers.Clear();
            _repository.Add(winner);
            throw new UniqueConstraintViolationException();
        };

        var result = await CreateHandler().HandleAsync(
            new CreateTransferCommand(Source, Destination, 100m, "key-1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(winner.Id, result.Value.Id);
    }
}
