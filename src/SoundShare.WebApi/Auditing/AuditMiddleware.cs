using System.Diagnostics;
using SoundShare.Domain.Audit;
using SoundShare.Infrastructure.Auditing;

namespace SoundShare.WebApi.Auditing;

internal sealed class AuditMiddleware(RequestDelegate next, AuditBuffer buffer, TimeProvider time)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var endpoint = context.GetEndpoint();
        var roomId = context.Request.RouteValues.TryGetValue("roomId", out var room) ? room?.ToString() : null;
        context.Response.OnCompleted(() =>
        {
            if (ShouldAudit(context))
            {
                buffer.Enqueue(Create(context, endpoint, roomId, Stopwatch.GetElapsedTime(started)));
            }

            return Task.CompletedTask;
        });
        await next(context);
    }

    private static bool ShouldAudit(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = context.Response.StatusCode;
        return !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method)
            || request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
            || status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden;
    }

    private AuditLog Create(HttpContext context, Endpoint? endpoint, string? roomId, TimeSpan elapsed)
    {
        var status = context.Response.StatusCode;
        var name = endpoint?.Metadata.GetMetadata<AuditActionAttribute>()?.Name
            ?? $"{context.Request.Method} {(endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unknown"}";
        if (status >= 400)
        {
            name += "Failed";
        }

        var userId = long.TryParse(context.User.FindFirst("sub")?.Value, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : (long?)null;
        return new AuditLog
        {
            CreatedAt = time.GetUtcNow().UtcDateTime,
            Source = "HTTP",
            UserId = userId,
            Action = Truncate(name, 64)!,
            EntityType = roomId is null ? null : "Room",
            EntityId = roomId,
            HttpMethod = context.Request.Method,
            Path = Truncate(context.Request.Path.Value, 512),
            StatusCode = status,
            IpAddress = Truncate(context.Connection.RemoteIpAddress?.ToString(), 45),
            UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512),
            CorrelationId = Truncate(context.TraceIdentifier, 64),
            DurationMs = (int)Math.Min(elapsed.TotalMilliseconds, int.MaxValue),
        };
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
