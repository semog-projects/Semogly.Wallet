using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Api.ErrorHandling;

internal static class ResultExtensions
{
    /// <summary>Converte um <see cref="Error"/> da Application em ProblemDetails com o status HTTP adequado.</summary>
    public static IResult ToProblem(this Error error) => TypedResults.Problem(
        title: error.Code,
        detail: error.Message,
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        });
}
