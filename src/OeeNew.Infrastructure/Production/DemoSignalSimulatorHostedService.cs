using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Production;

/// <summary>
/// Opt-in (config "Production:SimulateSignal") demo feed for environments that have no real PLC/gateway
/// pushing readings. Two modes, both re-ingesting on a timer so <see cref="MachineState.LastReportedAt"/>
/// never crosses the no-signal threshold:
/// <list type="bullet">
/// <item>Heartbeat (default, <see cref="ProductionOptions.RandomizeStatus"/> false): re-sends each
/// machine's current status unchanged, ticking at a third of the no-signal threshold so a single missed
/// tick can't tip a machine into no-signal.</item>
/// <item>Random (<see cref="ProductionOptions.RandomizeStatus"/> true): picks a random status each tick
/// at <see cref="ProductionOptions.SimulateIntervalSeconds"/> cadence — fabricated demo data, not a
/// heartbeat, for exercising a "live" dashboard without a real data source.</item>
/// </list>
/// Never enable either mode once a real data source exists: it would mask that source going quiet,
/// which no-signal detection (Story 2.3) exists to catch.
/// </summary>
public sealed class DemoSignalSimulatorHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductionOptions> options) : BackgroundService
{
    private static readonly MachineStatus[] AllStatuses = Enum.GetValues<MachineStatus>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.RandomizeStatus
            ? TimeSpan.FromSeconds(Math.Max(5, options.Value.SimulateIntervalSeconds))
            : TimeSpan.FromSeconds(Math.Max(5, options.Value.NoSignalThresholdSeconds / 3));
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
        var randomizeStatus = options.Value.RandomizeStatus;
        foreach (var state in states)
        {
            var status = randomizeStatus ? AllStatuses[Random.Shared.Next(AllStatuses.Length)] : state.Status;
            var reading = new SimulatedReading(state.MachineId, now, state.Counter + 1, status);
            await ingest.IngestAsync(CallerScope.Global, "Admin", reading, cancellationToken);
        }
    }

    private sealed record SimulatedReading(Guid MachineId, DateTimeOffset Timestamp, long Counter, MachineStatus Status)
        : IProductionDataSource;
}
