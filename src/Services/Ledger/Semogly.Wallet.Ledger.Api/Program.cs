using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Semogly.Wallet.Ledger.Api.Endpoints;
using Semogly.Wallet.Ledger.Api.ErrorHandling;
using Semogly.Wallet.Ledger.Application;
using Semogly.Wallet.Ledger.Infrastructure;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi(options => options.AddGatewayServer());

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.ApplyMigrationsAsync();
}

app.MapDefaultEndpoints();
app.MapLedgerEndpoints();

await app.RunAsync();
