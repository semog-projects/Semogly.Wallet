using Semogly.Wallet.Notifications.Application.Abstractions;
using Semogly.Wallet.Notifications.Application.Contacts;
using Semogly.Wallet.Notifications.Application.Notifications;
using Semogly.Wallet.Notifications.Application.Transfers;
using Semogly.Wallet.Notifications.Domain.Contacts;
using Semogly.Wallet.Notifications.Domain.Notifications;

namespace Semogly.Wallet.Notifications.UnitTests;

public class NotificationHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeContactRepository _contacts = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeSender _sender = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _bruno = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private NotificationDispatcher Dispatcher() => new(_notifications, _sender, _unitOfWork, _clock);

    private async Task RegisterAsync(Guid accountId, string name, string email) =>
        await new RegisterContactCommandHandler(_contacts, Dispatcher(), _clock)
            .HandleAsync(new RegisterContactCommand(accountId, name, email), Ct);

    [Fact]
    public async Task RegisterContact_ShouldStoreContactAndSendWelcome()
    {
        await RegisterAsync(_ana, "Ana Souza", "ana@email.com");

        Assert.Single(_contacts.Contacts);
        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(NotificationType.Welcome, sent.Type);
        Assert.StartsWith("Olá, Ana!", sent.Body, StringComparison.Ordinal);
        Assert.Equal(NotificationStatus.Sent, sent.Status);
    }

    [Fact]
    public async Task RegisterContact_WhenReprocessed_ShouldNotSendWelcomeTwice()
    {
        await RegisterAsync(_ana, "Ana Souza", "ana@email.com");
        await RegisterAsync(_ana, "Ana Souza", "ana@email.com");

        Assert.Single(_contacts.Contacts);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task TransferCompleted_ShouldNotifySenderAndReceiver()
    {
        await RegisterAsync(_ana, "Ana Souza", "ana@email.com");
        await RegisterAsync(_bruno, "Bruno Lima", "bruno@email.com");
        _sender.Sent.Clear();

        await new NotifyTransferCompletedCommandHandler(_contacts, Dispatcher(), _clock)
            .HandleAsync(new NotifyTransferCompletedCommand(Guid.NewGuid(), _ana, _bruno, 1_234.5m), Ct);

        Assert.Collection(
            _sender.Sent,
            sent =>
            {
                Assert.Equal(NotificationType.TransferSent, sent.Type);
                Assert.Equal("ana@email.com", sent.Recipient);
                Assert.Contains("R$ 1.234,50", sent.Body, StringComparison.Ordinal);
            },
            received =>
            {
                Assert.Equal(NotificationType.TransferReceived, received.Type);
                Assert.Equal("bruno@email.com", received.Recipient);
            });
    }

    [Fact]
    public async Task TransferFailed_ShouldNotifySenderWithReason()
    {
        await RegisterAsync(_ana, "Ana Souza", "ana@email.com");
        _sender.Sent.Clear();

        await new NotifyTransferFailedCommandHandler(_contacts, Dispatcher(), _clock)
            .HandleAsync(new NotifyTransferFailedCommand(Guid.NewGuid(), _ana, 50m, "Saldo insuficiente."), Ct);

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(NotificationType.TransferFailed, sent.Type);
        Assert.EndsWith("Motivo: Saldo insuficiente.", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferCompleted_WithUnknownContacts_ShouldSendNothing()
    {
        await new NotifyTransferCompletedCommandHandler(_contacts, Dispatcher(), _clock)
            .HandleAsync(new NotifyTransferCompletedCommand(Guid.NewGuid(), _ana, _bruno, 10m), Ct);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task WhenSenderFails_ShouldThrowSoTheMessageIsRetried()
    {
        _sender.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => RegisterAsync(_ana, "Ana Souza", "ana@email.com"));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private sealed class FakeContactRepository : IContactRepository
    {
        public List<Contact> Contacts { get; } = [];

        public Task<Contact?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Contacts.FirstOrDefault(c => c.AccountId == accountId));

        public void Add(Contact contact) => Contacts.Add(contact);
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        private readonly List<Notification> _stored = [];

        public Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken) =>
            Task.FromResult(_stored.Any(n => n.DeduplicationKey == deduplicationKey));

        public void Add(Notification notification) => _stored.Add(notification);
    }

    private sealed class FakeSender : INotificationSender
    {
        public List<Notification> Sent { get; } = [];
        public bool FailNext { get; set; }

        public Task SendAsync(Notification notification, CancellationToken cancellationToken)
        {
            if (FailNext)
            {
                throw new InvalidOperationException("Provedor indisponível.");
            }

            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
