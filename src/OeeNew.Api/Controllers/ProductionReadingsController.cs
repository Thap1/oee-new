using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Api.Controllers;

/// <summary>
/// The request DTO implements <see cref="IProductionDataSource"/> directly — no separate mapping
/// step — which is what makes "one domain path for every caller" (Story 2.1 AC #4) structurally true.
/// </summary>
public sealed record IngestReadingRequest(
    [Required] Guid MachineId,
    [Required] DateTimeOffset Timestamp,
    [Required] long Counter,
    [Required] MachineStatus Status) : IProductionDataSource;

/// <summary>Single ingestion endpoint for automatic and manual production readings (Story 2.1, FR-001/002/003, AD-3).</summary>
[ApiController]
[Authorize]
public sealed class ProductionReadingsController(IngestProductionReadingUseCase useCase) : ControllerBase
{
    [HttpPost("api/production/readings")]
    public async Task<IActionResult> Ingest([FromBody] IngestReadingRequest request, CancellationToken cancellationToken)
    {
        await useCase.IngestAsync(User.GetCallerScope(), CallerRole, request, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);
}
