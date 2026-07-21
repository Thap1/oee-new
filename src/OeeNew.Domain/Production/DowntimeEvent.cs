namespace OeeNew.Domain.Production;

/// <summary>
/// Opened/closed by machine status transitions during ingestion (Story 2.5) — <see cref="StartedAt"/>/
/// <see cref="EndedAt"/> reflect the actual machine-reported timestamps, not when an Operator happens
/// to tap a reason. <see cref="ReasonCodeId"/> is attached separately by the Operator (Reason Code
/// Picker) and may remain <c>null</c> if the machine resumes before a reason is picked — an accepted
/// "unattributed" downtime, not a defect (AD-2's synced business record).
/// </summary>
public sealed class DowntimeEvent
{
    public Guid Id { get; private set; }
    public Guid MachineId { get; private set; }
    public Guid? ReasonCodeId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    public bool IsOpen => EndedAt is null;

    public DowntimeEvent(Guid id, Guid machineId, DateTimeOffset startedAt)
    {
        Id = id;
        MachineId = machineId;
        StartedAt = startedAt;
    }

    /// <summary>Overwrites freely while open (an Operator correcting a wrong tap) — throws once the event has closed.</summary>
    public void AssignReason(Guid reasonCodeId)
    {
        if (!IsOpen)
        {
            throw new DowntimeEventNotOpenException();
        }

        ReasonCodeId = reasonCodeId;
    }

    /// <summary>No-op if already closed — a defensive guard; shouldn't trigger in practice given <see cref="MachineState"/>'s stale-reading guard upstream.</summary>
    public void Close(DateTimeOffset endedAt)
    {
        if (!IsOpen)
        {
            return;
        }

        EndedAt = endedAt;
    }
}
