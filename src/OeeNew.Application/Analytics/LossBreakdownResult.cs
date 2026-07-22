namespace OeeNew.Application.Analytics;

/// <summary>
/// Result of Story 3.1's loss pie chart query — 3 fixed OEE loss-category totals (AD-5, seconds) plus
/// two supplementary figures that are deliberately NOT part of the pie's 3-color proportions:
/// <see cref="UnattributedSeconds"/> (closed DowntimeEvents with no ReasonCode attached — Story 2.5
/// Dev Notes) and <see cref="QualityRejectQuantity"/> (reject count has no time value to convert into,
/// see Story 3.1 Dev Notes for why it isn't blended into the Quality slice).
/// </summary>
public sealed record LossBreakdownResult(
    Guid TargetId,
    LossBreakdownTargetType TargetType,
    long AvailabilitySeconds,
    long PerformanceSeconds,
    long QualitySeconds,
    long UnattributedSeconds,
    int QualityRejectQuantity);
