using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.Ledger.Domain.Accounts;
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
            bus.AddConsumer<DebitAccountConsumer>();
            bus.AddConsumer<CreditAccountConsumer>();
        });

        builder.Services.AddScoped<LedgerUnitOfWork>();
        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LedgerUnitOfWork>());
        builder.Services.AddScoped<ILedgerAccountRepository, MartenLedgerAccountRepository>();
        builder.Services.AddScoped<IStatementReadService, MartenStatementReadService>();
        builder.Services.AddScoped<ILedgerBookReadService, SqlLedgerBookReadService>();
        builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        return builder;
    }
}
