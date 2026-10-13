using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundShare.Application.Abstractions;
using SoundShare.Domain.Audit;

namespace SoundShare.Infrastructure.Auditing;

internal sealed partial class AuditWriterService(AuditBuffer queue, IServiceScopeFactory scopes, ILogger<AuditWriterService> logger) : BackgroundService
{
    private const int BatchSize = 100;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditLog>(BatchSize);
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < BatchSize && queue.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                }

                await FlushAsync(batch, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            while (queue.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<AuditLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAuditRepository>().AddRangeAsync(batch, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWriteFailed(logger, ex, batch.Count);
        }

        batch.Clear();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write {Count} audit entries")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception, int count);
}
