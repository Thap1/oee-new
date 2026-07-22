namespace OeeNew.Application.Analytics;

/// <summary>One ReasonCode's contribution to a tapped pie slice's drill-down (Story 3.2, AC #2).</summary>
public sealed record ReasonBreakdownItem(Guid ReasonCodeId, string ReasonCodeName, long DurationSeconds);
