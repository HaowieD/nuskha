using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nuskha.Infrastructure.Import;

public record ImportJob(string SourceId, string? Text, int Limit, Guid? UserId);

public class ImportQueue
{
    private readonly Channel<ImportJob> _channel = Channel.CreateBounded<ImportJob>(20);
    public bool TryEnqueue(ImportJob job) => _channel.Writer.TryWrite(job);
    public IAsyncEnumerable<ImportJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Runs queued imports one at a time in the background.</summary>
public class ImportWorker(ImportQueue queue, IServiceScopeFactory scopes, ILogger<ImportWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ImportService>();
                    await service.RunAsync(job.SourceId, new ImportQuery(job.Text, job.Limit), job.UserId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Import job for {Source} crashed", job.SourceId);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
