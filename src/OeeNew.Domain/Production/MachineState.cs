namespace OeeNew.Domain.Production;

/// <summary>
/// The latest known reading for a Machine (1:1 with <see cref="MasterData.Machine"/> — see AD-6/AD-3).
/// This is mutable runtime state, deliberately separate from <see cref="MasterData.Machine"/>'s static
/// master-data identity (Epic 1). Out-of-order network delivery is expected for ingestion (unlike
/// Epic 1's user-driven master-data writes), so <see cref="Apply"/> silently drops a reading that is
/// older than the one already stored, rather than letting the dashboard regress backward in time.
/// </summary>
public sealed class MachineState
{
    public Guid MachineId { get; private set; }
    public MachineStatus Status { get; private set; }
    public long Counter { get; private set; }
    public DateTimeOffset LastReportedAt { get; private set; }

    public MachineState(Guid machineId, MachineStatus status, long counter, DateTimeOffset lastReportedAt)
    {
        MachineId = machineId;
        Status = status;
        Counter = counter;
        LastReportedAt = lastReportedAt;
    }

    /// <summary>Applies a subsequent reading. Returns false (no-op) if <paramref name="reportedAt"/> is not strictly newer than the currently stored reading — a stale/out-of-order packet, not an error.</summary>
    public bool Apply(MachineStatus status, long counter, DateTimeOffset reportedAt)
    {
        if (reportedAt <= LastReportedAt)
        {
            return false;
        }

        Status = status;
        Counter = counter;
        LastReportedAt = reportedAt;
        return true;
    }
}
