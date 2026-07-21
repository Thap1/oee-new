using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record CreateReasonCodeRequest([Required] string Name, LossCategory? LossCategory);
public sealed record ReasonCodeResponse(Guid Id, Guid SiteId, string Name, LossCategory LossCategory, bool IsActive);

/// <summary>Reason Code CRUD under a Site (Story 1.5, FR-014). Reads: any authenticated role. Writes: Admin only (AC #3, NFR-5).</summary>
[ApiController]
[Authorize]
public sealed class ReasonCodesController(ReasonCodeManagementUseCase useCase) : ControllerBase
{
    [HttpGet("api/master-data/sites/{siteId:guid}/reason-codes")]
    public async Task<ActionResult<IReadOnlyList<ReasonCodeResponse>>> ListBySite(Guid siteId, CancellationToken cancellationToken)
    {
        var reasonCodes = await useCase.ListBySiteAsync(User.GetCallerScope(), siteId, cancellationToken);
        return Ok(reasonCodes.Select(ToResponse).ToList());
    }

    [HttpPost("api/master-data/sites/{siteId:guid}/reason-codes")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReasonCodeResponse>> Create(Guid siteId, [FromBody] CreateReasonCodeRequest request, CancellationToken cancellationToken)
    {
        var reasonCode = await useCase.CreateAsync(CallerRole, siteId, request.Name, request.LossCategory, cancellationToken);
        return CreatedAtAction(nameof(ListBySite), new { siteId }, ToResponse(reasonCode));
    }

    [HttpPut("api/master-data/reason-codes/{id:guid}/deactivate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReasonCodeResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var reasonCode = await useCase.DeactivateAsync(CallerRole, id, cancellationToken);
        return Ok(ToResponse(reasonCode));
    }

    [HttpDelete("api/master-data/reason-codes/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await useCase.DeleteAsync(CallerRole, id, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static ReasonCodeResponse ToResponse(ReasonCode reasonCode) =>
        new(reasonCode.Id, reasonCode.SiteId, reasonCode.Name, reasonCode.LossCategory, reasonCode.IsActive);
}
