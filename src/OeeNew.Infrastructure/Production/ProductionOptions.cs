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
}
