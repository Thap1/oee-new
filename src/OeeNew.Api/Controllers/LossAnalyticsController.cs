using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Analytics;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record LossAreaOptionResponse(Guid LineId, string LineName, Guid SiteId);

public sealed record LossBreakdownResponse(
    Guid TargetId,
    string TargetType,
    long AvailabilitySeconds,
    long PerformanceSeconds,
    long QualitySeconds,
    long UnattributedSeconds,
    int QualityRejectQuantity);

public sealed record ReasonBreakdownItemResponse(Guid ReasonCodeId, string ReasonCodeName, long DurationSeconds);

/// <summary>Loss pie chart data for the Dashboard (Story 3.1, FR-019/020/021). Any authenticated role may read.</summary>
[ApiController]
[Authorize]
public sealed class LossAnalyticsController(LossBreakdownQueryUseCase breakdownUseCase, LossAreaOptionsQueryUseCase areaOptionsUseCase) : ControllerBase
{
    [HttpGet("api/analytics/loss-areas")]
    public async Task<ActionResult<IReadOnlyList<LossAreaOptionResponse>>> ListAreas(CancellationToken cancellationToken)
    {
        var areas = await areaOptionsUseCase.ListAsync(User.GetCallerScope(), cancellationToken);
        return Ok(areas.Select(a => new LossAreaOptionResponse(a.LineId, a.LineName, a.SiteId)).ToList());
    }

    [HttpGet("api/analytics/loss-breakdown")]
    public async Task<ActionResult<LossBreakdownResponse>> GetBreakdown(
        [FromQuery] LossBreakdownTargetType targetType, [FromQuery] Guid targetId, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await breakdownUseCase.GetAsync(User.GetCallerScope(), targetType, targetId, date, cancellationToken);
        return Ok(new LossBreakdownResponse(
            result.TargetId,
            result.TargetType.ToString(),
            result.AvailabilitySeconds,
            result.PerformanceSeconds,
            result.QualitySeconds,
            result.UnattributedSeconds,
            result.QualityRejectQuantity));
    }

    [HttpGet("api/analytics/loss-breakdown/reasons")]
    public async Task<ActionResult<IReadOnlyList<ReasonBreakdownItemResponse>>> GetReasonBreakdown(
        [FromQuery] LossBreakdownTargetType targetType, [FromQuery] Guid targetId, [FromQuery] LossCategory lossCategory,
        [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var items = await breakdownUseCase.GetReasonBreakdownAsync(User.GetCallerScope(), targetType, targetId, lossCategory, date, cancellationToken);
        return Ok(items.Select(i => new ReasonBreakdownItemResponse(i.ReasonCodeId, i.ReasonCodeName, i.DurationSeconds)).ToList());
    }
}
