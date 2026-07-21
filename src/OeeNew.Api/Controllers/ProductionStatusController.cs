using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OeeNew.Api.Auth;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;
using OeeNew.Infrastructure.Production;

namespace OeeNew.Api.Controllers;

public sealed record MachineStatusResponse(
    Guid MachineId,
    string MachineName,
    Guid LineId,
    Guid SiteId,
    MachineStatus? Status,
    long? Counter,
    DateTimeOffset? LastReportedAt);

/// <summary>
/// Bundles the no-signal threshold (Story 2.3) alongside the machine list — one round trip gives the
/// dashboard everything it needs to compute staleness itself (a presentation-layer concern, FR-003).
/// </summary>
public sealed record MachineStatesResponse(int NoSignalThresholdSeconds, IReadOnlyList<MachineStatusResponse> Machines);

/// <summary>Current status of every Machine in the caller's scope (Story 2.2, FR-004/006; reused by Story 2.4).</summary>
[ApiController]
[Authorize]
public sealed class ProductionStatusController(MachineStatusQueryUseCase useCase, IOptions<ProductionOptions> productionOptions) : ControllerBase
{
    [HttpGet("api/production/machine-states")]
    public async Task<ActionResult<MachineStatesResponse>> ListMachineStates(CancellationToken cancellationToken)
    {
        var snapshots = await useCase.ListAsync(User.GetCallerScope(), cancellationToken);
        return Ok(new MachineStatesResponse(productionOptions.Value.NoSignalThresholdSeconds, snapshots.Select(ToResponse).ToList()));
    }

    private static MachineStatusResponse ToResponse(MachineStatusSnapshot snapshot) =>
        new(snapshot.MachineId, snapshot.MachineName, snapshot.LineId, snapshot.SiteId, snapshot.Status, snapshot.Counter, snapshot.LastReportedAt);
}
