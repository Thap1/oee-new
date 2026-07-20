namespace OeeNew.Domain.MasterData;

/// <summary>
/// Fixed 3-value taxonomy (AD-5) — never extend: Epic 3's pie chart and Epic 4's reports hardcode
/// exactly these three categories (and their colors, per DESIGN.md).
/// </summary>
public enum LossCategory
{
    AvailabilityLoss,
    PerformanceLoss,
    QualityLoss,
}
