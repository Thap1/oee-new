using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Api.Errors;
using OeeNew.Application.Reports;

namespace OeeNew.Api.Controllers;

public sealed record OeeReportResponse(
    string PeriodType,
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
    Guid? TopDowntimeReasonCodeId,
    string? TopDowntimeReasonName,
    long? TopDowntimeReasonSeconds);

/// <summary>Aggregated OEE report by Shift/Day/Week (Story 4.1, FR-016). Manager/Viewer/Admin only — Operator is excluded (AC #3).</summary>
[ApiController]
[Authorize(Policy = "ReportsAccess")]
public sealed class ReportsController(OeeReportQueryUseCase reportUseCase) : ControllerBase
{
    [HttpGet("api/reports/oee")]
    public async Task<ActionResult<OeeReportResponse>> GetOeeReport(
        [FromQuery] ReportPeriodType periodType, [FromQuery] DateOnly referenceDate, [FromQuery] Guid? shiftScheduleId,
        [FromQuery] ReportFilterTargetType? filterType, [FromQuery] Guid? filterId, CancellationToken cancellationToken)
    {
        // Code-review fix: default enum query-string binding accepts any integer that parses to the
        // enum's underlying type (e.g. periodType=99), not just defined members — without this check, an
        // out-of-range value sails past the paired-argument checks below and hits ResolvePeriod's/
        // ResolveFilterMachinesAsync's `default: throw ArgumentOutOfRangeException`, which isn't mapped by
        // ApiExceptionHandler and surfaces as a 500 instead of a 400.
        if (!Enum.IsDefined(periodType))
        {
            return BadRequest(new ApiErrorResponse { Code = "VALIDATION_ERROR", Message = "periodType is not a recognized value." });
        }

        if (filterType is { } definedFilterType && !Enum.IsDefined(definedFilterType))
        {
            return BadRequest(new ApiErrorResponse { Code = "VALIDATION_ERROR", Message = "filterType is not a recognized value." });
        }

        if ((periodType == ReportPeriodType.Shift) != (shiftScheduleId is not null))
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "shiftScheduleId is required when periodType is Shift, and must be omitted otherwise.",
            });
        }

        if (filterType.HasValue != filterId.HasValue)
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "filterType and filterId must be provided together.",
            });
        }

        var result = await reportUseCase.GetReportAsync(
            User.GetCallerScope(), periodType, referenceDate, shiftScheduleId, filterType, filterId, cancellationToken);
        return Ok(new OeeReportResponse(
            result.PeriodType.ToString(),
            result.PeriodStart,
            result.PeriodEnd,
            result.AvailabilityPercent,
            result.PerformancePercent,
            result.QualityPercent,
            result.OeePercent,
            result.AvailabilityLossSeconds,
            result.PerformanceLossSeconds,
            result.QualityLossSeconds,
            result.UnattributedSeconds,
            result.QualityRejectQuantity,
            result.TopDowntimeReasonCodeId,
            result.TopDowntimeReasonName,
            result.TopDowntimeReasonSeconds));
    }
}
