namespace OeeNew.Domain.Production;

/// <summary>Thrown when assigning a reason to a `DowntimeEvent` that has already closed — the machine isn't currently down, there's nothing to attach a reason to.</summary>
public sealed class DowntimeEventNotOpenException() : Exception("This downtime event is no longer open.");
