namespace OeeNew.Application.Reports;

/// <summary>
/// One UTC calendar day of the Dashboard's OEE trend line. Same time-based-loss proxy formula as
/// <see cref="OeeReportResult"/> — see <see cref="OeeReportQueryUseCase"/> for why it is not a
/// textbook count-based OEE.
/// </summary>
public sealed record OeeTrendPoint(
    DateOnly Date,
    double AvailabilityPercent,
    double PerformancePercent,
    double QualityPercent,
    double OeePercent);
