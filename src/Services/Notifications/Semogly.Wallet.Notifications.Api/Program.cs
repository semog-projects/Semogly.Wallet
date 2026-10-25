using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Semogly.Wallet.Notifications.Api.Endpoints;
using Semogly.Wallet.Notifications.Api.ErrorHandling;
using Semogly.Wallet.Notifications.Application;
using Semogly.Wallet.Notifications.Infrastructure;
using Semogly.Wallet.Notifications.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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
app.MapNotificationsEndpoints();

await app.RunAsync();
