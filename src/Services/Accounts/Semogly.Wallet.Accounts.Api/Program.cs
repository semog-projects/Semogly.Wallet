using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Semogly.Wallet.Accounts.Api.Features;
using Semogly.Wallet.Accounts.Api.Features.ApplyLedgerEntries;
using Semogly.Wallet.Accounts.Api.Infrastructure;
using Semogly.Wallet.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Connection strings injetadas pelo AppHost do Aspire
builder.AddNpgsqlDbContext<AccountsDbContext>("accounts-db");
builder.AddRedisDistributedCache("redis");

builder.Services.AddHybridCache(options => options.DefaultEntryOptions = new()
{
    Expiration = TimeSpan.FromMinutes(5),
    LocalCacheExpiration = TimeSpan.FromMinutes(1),
});

builder.AddMessaging<AccountsDbContext>(bus =>
{
    bus.AddConsumer<AccountDebitedConsumer>();
    bus.AddConsumer<AccountCreditedConsumer>();
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.ApplyMigrationsAsync();
}

app.MapDefaultEndpoints();
app.MapAccountsEndpoints();

await app.RunAsync();
