using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using SoundShare.Application.Common;
using SoundShare.Domain.Common;

namespace SoundShare.WebApi.Errors;

internal sealed partial class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly HashSet<int> UnavailableCodes = [1033, 1034, 1089, 3113, 3114, 12170, 12514, 12528, 12537, 12541, 12543, 50000, 50201];

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, type) = Classify(exception);
        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = status < 500 ? exception.Message : null,
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (exception is ValidationException validation)
        {
            problem.Detail = "One or more validation errors occurred.";
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static (int Status, string Title, string Type) Classify(Exception exception) => exception switch
    {
        ValidationException => (400, "Validation failed", "validation-error"),
        BadHttpRequestException => (400, "Bad request", "bad-request"),
        AuthenticationFailedException => (401, "Unauthorized", "unauthorized"),
        AccountBlockedException => (403, "Account blocked", "account-blocked"),
        ForbiddenException => (403, "Forbidden", "forbidden"),
        NotFoundException => (404, "Not found", "not-found"),
        ConflictException or DomainException => (409, "Conflict", "conflict"),
        DbUpdateConcurrencyException => (409, "Conflict", "concurrency-conflict"),
        DbUpdateException { InnerException: OracleException { Number: 1 } } => (409, "Conflict", "duplicate"),
        OracleException oracle when UnavailableCodes.Contains(oracle.Number) => (503, "Service unavailable", "service-unavailable"),
        InvalidOperationException { InnerException: OracleException inner } when UnavailableCodes.Contains(inner.Number) => (503, "Service unavailable", "service-unavailable"),
        _ => (500, "Internal server error", "internal-error"),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
