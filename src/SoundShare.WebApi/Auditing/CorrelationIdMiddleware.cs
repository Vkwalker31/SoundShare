using System.Text.RegularExpressions;

namespace SoundShare.WebApi.Auditing;

internal sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        await next(context);
    }

    private static bool IsValid(string value) => value.Length is > 0 and <= 64 && SafeId().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeId();
}
