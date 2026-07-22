using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Production;

/// <summary>
/// Opt-in (config "Production:SimulateSignal") heartbeat for demo/deploy environments that have no
/// real PLC/gateway pushing readings — re-ingests every machine's current status on a timer so
/// <see cref="MachineState.LastReportedAt"/> never crosses the no-signal threshold. Ticks at a third
/// of that threshold so a single missed tick can't tip a machine into no-signal. Never enable this
/// once a real data source exists: it would mask that source going quiet, which no-signal detection
/// (Story 2.3) exists to catch.
/// </summary>
public sealed class DemoSignalSimulatorHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductionOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.NoSignalThresholdSeconds / 3));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var machines = scope.ServiceProvider.GetRequiredService<IMachineRepository>();
        var machineStates = scope.ServiceProvider.GetRequiredService<IMachineStateRepository>();
        var ingest = scope.ServiceProvider.GetRequiredService<IngestProductionReadingUseCase>();

        var allMachines = await machines.ListByScopeAsync(CallerScope.Global, cancellationToken);
        var machineIds = allMachines.Select(m => m.Id).ToList();
        // Only machines that have reported at least once (Story 2.2 skeleton state is intentional
        // for never-configured machines — this simulator keeps existing signal alive, not fabricates it).
        var states = await machineStates.ListByMachineIdsAsync(machineIds, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var state in states)
        {
            var reading = new SimulatedReading(state.MachineId, now, state.Counter + 1, state.Status);
            await ingest.IngestAsync(CallerScope.Global, "Admin", reading, cancellationToken);
        }
    }

    private sealed record SimulatedReading(Guid MachineId, DateTimeOffset Timestamp, long Counter, MachineStatus Status)
        : IProductionDataSource;
}
