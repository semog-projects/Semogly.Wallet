using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Accounts.Api.Domain;
using Semogly.Wallet.Accounts.Api.Infrastructure;
using Semogly.Wallet.Contracts.Accounts;

namespace Semogly.Wallet.Accounts.Api.Features.CreateAccount;

public sealed record CreateAccountRequest(
    [Required, StringLength(150, MinimumLength = 3)] string OwnerName,
    [Required, StringLength(14, MinimumLength = 11)] string Document,
    [Required, EmailAddress, StringLength(254)] string Email);

internal static class CreateAccountEndpoint
{
    public static RouteGroupBuilder MapCreateAccount(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateAccount")
            .WithSummary("Abre uma nova conta na carteira");

        return group;
    }

    private static async Task<Results<CreatedAtRoute<AccountResponse>, ProblemHttpResult>> HandleAsync(
        CreateAccountRequest request,
        AccountsDbContext db,
        IPublishEndpoint publisher,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = Account.Open(request.OwnerName, request.Document, request.Email, clock.GetUtcNow());

        if (await db.Accounts.AnyAsync(a => a.Document == account.Document, cancellationToken))
        {
            return TypedResults.Problem(
                title: "Conta já existente",
                detail: "Já existe uma conta para este documento.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.Accounts.Add(account);

        // Com o Bus Outbox, o evento é gravado na mesma transação do SaveChanges
        // e só é enviado ao RabbitMQ depois do commit.
        await publisher.Publish(
            new AccountCreated(account.Id, account.OwnerName, account.Email, account.Currency, account.CreatedAt),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.CreatedAtRoute(AccountResponse.From(account), "GetAccount", new { id = account.Id });
    }
}
