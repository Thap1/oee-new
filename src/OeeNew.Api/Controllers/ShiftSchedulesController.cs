using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record CreateShiftScheduleRequest([Required] string Name, Guid? LineId, TimeOnly StartTime, TimeOnly EndTime);
public sealed record RescheduleShiftScheduleRequest([Required] string Name, TimeOnly StartTime, TimeOnly EndTime);
public sealed record ShiftScheduleResponse(Guid Id, Guid SiteId, Guid? LineId, string Name, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>Shift Schedule CRUD under a Site (Story 1.3, FR-012). Reads: any authenticated role. Writes: Admin only (AC #3, NFR-5).</summary>
[ApiController]
[Authorize]
public sealed class ShiftSchedulesController(ShiftScheduleManagementUseCase useCase) : ControllerBase
{
    [HttpGet("api/master-data/sites/{siteId:guid}/shift-schedules")]
    public async Task<ActionResult<IReadOnlyList<ShiftScheduleResponse>>> ListBySite(Guid siteId, CancellationToken cancellationToken)
    {
        var shifts = await useCase.ListBySiteAsync(siteId, cancellationToken);
        return Ok(shifts.Select(ToResponse).ToList());
    }

    [HttpPost("api/master-data/sites/{siteId:guid}/shift-schedules")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ShiftScheduleResponse>> Create(Guid siteId, [FromBody] CreateShiftScheduleRequest request, CancellationToken cancellationToken)
    {
        var shift = await useCase.CreateAsync(CallerRole, siteId, request.LineId, request.Name, request.StartTime, request.EndTime, cancellationToken);
        return CreatedAtAction(nameof(ListBySite), new { siteId }, ToResponse(shift));
    }

    [HttpPut("api/master-data/shift-schedules/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ShiftScheduleResponse>> Reschedule(Guid id, [FromBody] RescheduleShiftScheduleRequest request, CancellationToken cancellationToken)
    {
        var shift = await useCase.RescheduleAsync(CallerRole, id, request.Name, request.StartTime, request.EndTime, cancellationToken);
        return Ok(ToResponse(shift));
    }

    [HttpDelete("api/master-data/shift-schedules/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await useCase.DeleteAsync(CallerRole, id, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static ShiftScheduleResponse ToResponse(ShiftSchedule shift) =>
        new(shift.Id, shift.SiteId, shift.LineId, shift.Name, shift.StartTime, shift.EndTime);
}
