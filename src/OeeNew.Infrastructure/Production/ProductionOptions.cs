namespace OeeNew.Infrastructure.Production;

/// <summary>
/// Bound from configuration section "Production". <see cref="NoSignalThresholdSeconds"/> is the single
/// source of truth for no-signal detection (Story 2.3) — a config value, not a hardcoded FE constant,
/// so it can be tuned without a code change. 60s is a placeholder (`[ASSUMPTION]`, no real machine
/// cadence known yet).
/// </summary>
public sealed class ProductionOptions
{
    public const string SectionName = "Production";

    public int NoSignalThresholdSeconds { get; set; } = 60;

    /// <summary>Opt-in: runs <see cref="DemoSignalSimulatorHostedService"/> to fake a live data feed
    /// when no real PLC/gateway is connected (demo/deploy environments only).</summary>
    public bool SimulateSignal { get; set; }

    /// <summary>When true, <see cref="DemoSignalSimulatorHostedService"/> picks a random <see cref="MachineStatus"/>
    /// each tick instead of re-sending the machine's current status — fabricated demo data, not a
    /// heartbeat. Ignored unless <see cref="SimulateSignal"/> is also true.</summary>
    public bool RandomizeStatus { get; set; }

    /// <summary>Tick interval in seconds while <see cref="RandomizeStatus"/> is true. Unlike the heartbeat
    /// interval (tied to <see cref="NoSignalThresholdSeconds"/> for no-signal safety), this is just a demo
    /// cadence — defaults to 60s.</summary>
    public int SimulateIntervalSeconds { get; set; } = 60;
}
