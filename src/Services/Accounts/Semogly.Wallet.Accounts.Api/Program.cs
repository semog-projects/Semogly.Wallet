using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Semogly.Wallet.Accounts.Api.Endpoints;
using Semogly.Wallet.Accounts.Api.ErrorHandling;
using Semogly.Wallet.Accounts.Application;
using Semogly.Wallet.Accounts.Infrastructure;
using Semogly.Wallet.Accounts.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Números só como número JSON: contrato estrito (e tipos limpos nos clientes gerados)
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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
app.MapAccountsEndpoints();

await app.RunAsync();
