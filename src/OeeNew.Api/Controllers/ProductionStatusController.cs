using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Api.Controllers;

public sealed record MachineStatusResponse(
    Guid MachineId,
    string MachineName,
    Guid LineId,
    MachineStatus? Status,
    long? Counter,
    DateTimeOffset? LastReportedAt);

/// <summary>Current status of every Machine in the caller's scope (Story 2.2, FR-004/006; reused by Story 2.4).</summary>
[ApiController]
[Authorize]
public sealed class ProductionStatusController(MachineStatusQueryUseCase useCase) : ControllerBase
{
    [HttpGet("api/production/machine-states")]
    public async Task<ActionResult<IReadOnlyList<MachineStatusResponse>>> ListMachineStates(CancellationToken cancellationToken)
    {
        var snapshots = await useCase.ListAsync(User.GetCallerScope(), cancellationToken);
        return Ok(snapshots.Select(ToResponse).ToList());
    }

    private static MachineStatusResponse ToResponse(MachineStatusSnapshot snapshot) =>
        new(snapshot.MachineId, snapshot.MachineName, snapshot.LineId, snapshot.Status, snapshot.Counter, snapshot.LastReportedAt);
}
