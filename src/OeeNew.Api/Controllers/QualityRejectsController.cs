using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.Production;

namespace OeeNew.Api.Controllers;

public sealed record RecordQualityRejectRequest([Required][Range(1, int.MaxValue)] int Quantity);

/// <summary>Records basic reject/scrap quantity for a Machine (Story 2.6, FR-010).</summary>
[ApiController]
[Authorize]
public sealed class QualityRejectsController(RecordQualityRejectUseCase useCase) : ControllerBase
{
    [HttpPost("api/production/machines/{machineId:guid}/quality-rejects")]
    public async Task<IActionResult> Record(Guid machineId, [FromBody] RecordQualityRejectRequest request, CancellationToken cancellationToken)
    {
        await useCase.RecordAsync(User.GetCallerScope(), CallerRole, machineId, request.Quantity, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);
}
