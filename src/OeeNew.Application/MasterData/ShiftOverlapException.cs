namespace OeeNew.Application.MasterData;

/// <summary>
/// Thrown when a Shift Schedule's time window overlaps an existing shift in the same Site+Line
/// scope (Story 1.3 AC #2, `[ASSUMPTION]` — avoids double-counting production time in reports).
/// </summary>
public sealed class ShiftOverlapException(string conflictingShiftName)
    : Exception($"Overlaps with existing shift '{conflictingShiftName}'.")
{
    public string ConflictingShiftName { get; } = conflictingShiftName;
}
