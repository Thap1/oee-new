using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Cross-aggregate read projection (Machine + its latest MachineState) for the dashboard (Story 2.2).
/// <see cref="Status"/>/<see cref="Counter"/>/<see cref="LastReportedAt"/> are null when the machine
/// has never reported yet — a normal case, not an error (rendered as a skeleton, not a failure).
/// </summary>
public sealed record MachineStatusSnapshot(
    Guid MachineId,
    string MachineName,
    Guid LineId,
    MachineStatus? Status,
    long? Counter,
    DateTimeOffset? LastReportedAt);
