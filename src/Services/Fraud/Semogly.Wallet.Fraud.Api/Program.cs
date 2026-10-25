using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Semogly.Wallet.Fraud.Api.Endpoints;
using Semogly.Wallet.Fraud.Api.ErrorHandling;
using Semogly.Wallet.Fraud.Application;
using Semogly.Wallet.Fraud.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi(options => options.AddGatewayServer());

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapFraudEndpoints();

await app.RunAsync();
