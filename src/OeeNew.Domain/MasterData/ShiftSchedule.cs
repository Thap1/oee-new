namespace OeeNew.Domain.MasterData;

/// <summary>
/// Defines a recurring daily work shift for a Site, optionally scoped to one of its Lines
/// (Architecture Spine — Core-Entity ERD: SITE ||--o{ SHIFT_SCHEDULE). <see cref="StartTime"/> and
/// <see cref="EndTime"/> are time-of-day, not absolute instants — the shift recurs every day, which
/// is why an EF Core `time` column is used instead of `timestamp` (Story 1.3).
/// </summary>
public sealed class ShiftSchedule
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid? LineId { get; private set; }
    public string Name { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    public ShiftSchedule(Guid id, Guid siteId, Guid? lineId, string name, TimeOnly startTime, TimeOnly endTime)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Shift schedule requires a valid parent Site.", nameof(siteId));
        }

        ValidateTimes(startTime, endTime);

        Id = id;
        SiteId = siteId;
        LineId = lineId;
        Name = ValidateName(name);
        StartTime = startTime;
        EndTime = endTime;
    }

    public void Rename(string name) => Name = ValidateName(name);

    public void Reschedule(TimeOnly startTime, TimeOnly endTime)
    {
        ValidateTimes(startTime, endTime);
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>
    /// True if this shift's daily time-of-day window overlaps <paramref name="other"/>'s, within the
    /// same Site+Line scope (AC #2). Handles overnight shifts (<see cref="EndTime"/> &lt;
    /// <see cref="StartTime"/>, e.g. 22:00–06:00) by splitting each shift into its wrapped-midnight
    /// segments before comparing — a plain start/end comparison would miss overlaps that cross midnight.
    /// </summary>
    public bool OverlapsWith(ShiftSchedule other)
    {
        if (SiteId != other.SiteId || LineId != other.LineId)
        {
            return false;
        }

        foreach (var (aStart, aEnd) in ToMinuteSegments())
        {
            foreach (var (bStart, bEnd) in other.ToMinuteSegments())
            {
                if (aStart < bEnd && bStart < aEnd)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<(int Start, int End)> ToMinuteSegments()
    {
        var start = StartTime.Hour * 60 + StartTime.Minute;
        var end = EndTime.Hour * 60 + EndTime.Minute;

        if (start < end)
        {
            yield return (start, end);
        }
        else
        {
            yield return (start, 24 * 60);
            yield return (0, end);
        }
    }

    private static void ValidateTimes(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime == endTime)
        {
            throw new MasterDataValidationException("Shift start and end time cannot be equal.");
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MasterDataValidationException("Shift schedule name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new MasterDataValidationException($"Shift schedule name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
