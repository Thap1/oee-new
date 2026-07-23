namespace OeeNew.Infrastructure.Sync;

/// <summary>
/// Bound from configuration section "Sync". Opt-in (<see cref="Enabled"/> defaults false) for the same
/// reason <c>ProductionOptions.SimulateSignal</c> is opt-in: most dev/demo runs are a single standalone
/// instance with no reachable Central counterpart, so a background push loop shouldn't spin up and
/// immediately fail every tick against a placeholder <see cref="CentralBaseUrl"/>.
/// </summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public bool Enabled { get; set; }
    public string? CentralBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public int IntervalSeconds { get; set; } = 60;
}
