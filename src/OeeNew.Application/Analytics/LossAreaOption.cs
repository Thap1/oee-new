namespace OeeNew.Application.Analytics;

/// <summary>One Production Area (Line) option for Story 3.1's "view by Area" dropdown.</summary>
public sealed record LossAreaOption(Guid LineId, string LineName, Guid SiteId);
