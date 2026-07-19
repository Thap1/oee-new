using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record CreateLineRequest([Required] string Name);
public sealed record RenameLineRequest([Required] string Name);
public sealed record LineResponse(Guid Id, string Name, Guid SiteId);

/// <summary>Line CRUD under a Site (Story 1.2, FR-011). Reads: any authenticated role. Writes: Admin only (AC #4, NFR-5).</summary>
[ApiController]
[Authorize]
public sealed class LinesController(LineManagementUseCase useCase) : ControllerBase
{
    [HttpGet("api/master-data/sites/{siteId:guid}/lines")]
    public async Task<ActionResult<IReadOnlyList<LineResponse>>> ListBySite(Guid siteId, CancellationToken cancellationToken)
    {
        var lines = await useCase.ListBySiteAsync(siteId, cancellationToken);
        return Ok(lines.Select(ToResponse).ToList());
    }

    [HttpPost("api/master-data/sites/{siteId:guid}/lines")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<LineResponse>> Create(Guid siteId, [FromBody] CreateLineRequest request, CancellationToken cancellationToken)
    {
        var line = await useCase.CreateAsync(CallerRole, siteId, request.Name, cancellationToken);
        return CreatedAtAction(nameof(ListBySite), new { siteId }, ToResponse(line));
    }

    [HttpPut("api/master-data/lines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<LineResponse>> Rename(Guid id, [FromBody] RenameLineRequest request, CancellationToken cancellationToken)
    {
        var line = await useCase.RenameAsync(CallerRole, id, request.Name, cancellationToken);
        return Ok(ToResponse(line));
    }

    [HttpDelete("api/master-data/lines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await useCase.DeleteAsync(CallerRole, id, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static LineResponse ToResponse(Line line) => new(line.Id, line.Name, line.SiteId);
}
