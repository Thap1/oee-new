using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record CreateSiteRequest([Required] string Name);
public sealed record RenameSiteRequest([Required] string Name);
public sealed record SiteResponse(Guid Id, string Name);

/// <summary>Site CRUD (Story 1.2, FR-011). Reads: any authenticated role. Writes: Admin only (AC #4, NFR-5).</summary>
[ApiController]
[Route("api/master-data/sites")]
[Authorize]
public sealed class SitesController(SiteManagementUseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SiteResponse>>> List(CancellationToken cancellationToken)
    {
        var sites = await useCase.ListAsync(cancellationToken);
        return Ok(sites.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<SiteResponse>> Create([FromBody] CreateSiteRequest request, CancellationToken cancellationToken)
    {
        var site = await useCase.CreateAsync(CallerRole, request.Name, cancellationToken);
        return CreatedAtAction(nameof(List), null, ToResponse(site));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<SiteResponse>> Rename(Guid id, [FromBody] RenameSiteRequest request, CancellationToken cancellationToken)
    {
        var site = await useCase.RenameAsync(CallerRole, id, request.Name, cancellationToken);
        return Ok(ToResponse(site));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await useCase.DeleteAsync(CallerRole, id, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static SiteResponse ToResponse(Site site) => new(site.Id, site.Name);
}
