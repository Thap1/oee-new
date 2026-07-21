using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.Production;

namespace OeeNew.Api.Controllers;

public sealed record AttachDowntimeReasonRequest([Required] Guid ReasonCodeId);

/// <summary>Attaches a Reason Code to a machine's currently-open DowntimeEvent (Story 2.5, FR-008/009).</summary>
[ApiController]
[Authorize]
public sealed class DowntimeReasonController(RecordDowntimeReasonUseCase useCase) : ControllerBase
{
    [HttpPost("api/production/machines/{machineId:guid}/downtime-reason")]
    public async Task<IActionResult> AttachReason(Guid machineId, [FromBody] AttachDowntimeReasonRequest request, CancellationToken cancellationToken)
    {
        await useCase.AttachReasonAsync(User.GetCallerScope(), CallerRole, machineId, request.ReasonCodeId, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);
}
