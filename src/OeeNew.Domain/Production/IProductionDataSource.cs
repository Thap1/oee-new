namespace OeeNew.Domain.Production;

/// <summary>
/// The one normalized shape Domain/Application ever see for a machine reading (Architecture Spine
/// AD-3). Any caller — a real protocol adapter, a manual-entry request, a simulated script — must
/// produce this shape; the protocol/transport detail stays in Infrastructure/Api, never leaking up.
/// <see cref="Counter"/> is cumulative (a running total at the time of the reading), never a delta.
/// </summary>
public interface IProductionDataSource
{
    Guid MachineId { get; }
    DateTimeOffset Timestamp { get; }
    long Counter { get; }
    MachineStatus Status { get; }
}
