using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OeeNew.Application.Sync;

namespace OeeNew.Infrastructure.Sync;

/// <summary>
/// Opt-in (config "Sync:Enabled") periodic push loop for a Site instance (Story 5.1, AC #1/#2) — same
/// <see cref="BackgroundService"/>/<see cref="IServiceScopeFactory"/>/<see cref="PeriodicTimer"/> shape as
/// <c>DemoSignalSimulatorHostedService</c>. Never lets a failed/exception-throwing push crash the loop:
/// that's the concrete mechanism behind AC #2's "site vẫn tiếp tục vận hành đầy đủ" — a lost connection
/// to Central must never take down local ingestion/dashboard/downtime.
/// </summary>
public sealed class SyncPushHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    ILogger<SyncPushHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<PushSyncBatchUseCase>();
            var pushed = await useCase.RunOnceAsync(cancellationToken);
            if (!pushed)
            {
                logger.LogWarning("Sync push cycle did not reach Central; will retry next tick.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync push cycle failed unexpectedly; will retry next tick.");
        }
    }
}
