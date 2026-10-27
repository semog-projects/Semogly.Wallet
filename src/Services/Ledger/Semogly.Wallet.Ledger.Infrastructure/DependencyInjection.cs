using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Domain.Transfers;
using Semogly.Wallet.Ledger.Infrastructure.EventStore;
using Semogly.Wallet.Ledger.Infrastructure.Messaging;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;
using Semogly.Wallet.Messaging;

namespace Semogly.Wallet.Ledger.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionName = "ledger-db";

    /// <summary>Schema do Postgres com as tabelas do Marten (eventos e documentos).</summary>
    public const string EventStoreSchema = "ledger";

    private const int PostingConcurrency = 4;

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Sem retry automático do EF: ele não suporta as transações explícitas compartilhadas com o Marten
        builder.AddNpgsqlDbContext<LedgerDbContext>(ConnectionName, settings => settings.DisableRetry = true);
        builder.AddNpgsqlDataSource(ConnectionName);

        var marten = builder.Services.AddMarten(options =>
            {
                options.DatabaseSchemaName = EventStoreSchema;
                options.UseSystemTextJsonForSerialization();

                // Rich: sequência e versão do evento já disponíveis nas projeções inline (o extrato ordena por Sequence)
                options.Events.AppendMode = EventAppendMode.Rich;
                options.Events.AddEventTypes([typeof(LedgerAccountOpened), typeof(FundsDebited), typeof(FundsCredited)]);

                // Streams das transferências: árbitro dos timeouts da saga (sem valor próprio, fora do extrato)
                options.Events.AddEventTypes(
                [
                    typeof(TransferDebitRecorded), typeof(TransferCreditRecorded),
                    typeof(TransferRefundRecorded), typeof(TransferCancelled),
                ]);
                options.Projections.Add<StatementProjection>(ProjectionLifecycle.Inline);
                options.Schema.For<StatementEntry>().Index(e => e.AccountId);
            })
            .UseNpgsqlDataSource()
            .UseLightweightSessions();

        if (builder.Environment.IsDevelopment())
        {
            // Em produção, o schema do Marten deve ser aplicado em uma etapa separada do deploy
            marten.ApplyAllDatabaseChangesOnStartup();
        }

        builder.AddMessaging<LedgerDbContext>("ledger", bus =>
        {
            bus.AddConsumer<AccountCreatedConsumer>();
            // Lançamentos de transferência disputam streams (contas e partições da Clearing): paralelismo alto só
            // gera conflitos de concorrência e retries. Poucos lançamentos simultâneos por fila mantêm a vazão
            bus.AddConsumer<DebitAccountConsumer>().Endpoint(e => e.ConcurrentMessageLimit = PostingConcurrency);
            bus.AddConsumer<CreditAccountConsumer>().Endpoint(e => e.ConcurrentMessageLimit = PostingConcurrency);
            bus.AddConsumer<CancelTransferPostingConsumer>().Endpoint(e => e.ConcurrentMessageLimit = PostingConcurrency);
        });

        builder.Services.AddScoped<LedgerUnitOfWork>();
        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LedgerUnitOfWork>());
        builder.Services.AddScoped<ILedgerAccountRepository, MartenLedgerAccountRepository>();
        builder.Services.AddScoped<ITransferPostingRepository, MartenTransferPostingRepository>();
        builder.Services.AddScoped<IDepositReceiptStore, MartenDepositReceiptStore>();
        builder.Services.AddScoped<IStatementReadService, MartenStatementReadService>();
        builder.Services.AddScoped<ILedgerBookReadService, SqlLedgerBookReadService>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }
}
