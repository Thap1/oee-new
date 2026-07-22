namespace OeeNew.Application.Reports;

/// <summary>
/// Aggregated OEE report for one period window (Story 4.1, FR-016). A time-based-loss proxy — see
/// <see cref="OeeReportQueryUseCase"/> for the formula and why this is not a textbook count-based OEE.
/// </summary>
public sealed record OeeReportResult(
    ReportPeriodType PeriodType,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    double AvailabilityPercent,
    double PerformancePercent,
    double QualityPercent,
    double OeePercent,
    long AvailabilityLossSeconds,
    long PerformanceLossSeconds,
    long QualityLossSeconds,
    long UnattributedSeconds,
    int QualityRejectQuantity,
    Guid? TopDowntimeReasonCodeId = null,
    string? TopDowntimeReasonName = null,
    long? TopDowntimeReasonSeconds = null);
