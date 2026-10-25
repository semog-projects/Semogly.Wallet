using Microsoft.AspNetCore.Diagnostics;
using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Fraud.Api.ErrorHandling;

/// <summary>Converte <see cref="DomainException"/> em ProblemDetails com HTTP 422.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Regra de negócio violada",
                Detail = domainException.Message,
                Status = StatusCodes.Status422UnprocessableEntity,
            },
        });
    }
}
