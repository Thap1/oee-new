using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Api.Controllers;

public sealed record CreateMachineRequest([Required] string Name);
public sealed record RenameMachineRequest([Required] string Name);
public sealed record MachineResponse(Guid Id, string Name, Guid LineId);

/// <summary>Machine CRUD under a Line (Story 1.2, FR-011). Reads: any authenticated role. Writes: Admin only (AC #4, NFR-5).</summary>
[ApiController]
[Authorize]
public sealed class MachinesController(MachineManagementUseCase useCase) : ControllerBase
{
    [HttpGet("api/master-data/lines/{lineId:guid}/machines")]
    public async Task<ActionResult<IReadOnlyList<MachineResponse>>> ListByLine(Guid lineId, CancellationToken cancellationToken)
    {
        var machines = await useCase.ListByLineAsync(User.GetCallerScope(), lineId, cancellationToken);
        return Ok(machines.Select(ToResponse).ToList());
    }

    [HttpPost("api/master-data/lines/{lineId:guid}/machines")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MachineResponse>> Create(Guid lineId, [FromBody] CreateMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await useCase.CreateAsync(CallerRole, lineId, request.Name, cancellationToken);
        return CreatedAtAction(nameof(ListByLine), new { lineId }, ToResponse(machine));
    }

    [HttpPut("api/master-data/machines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MachineResponse>> Rename(Guid id, [FromBody] RenameMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await useCase.RenameAsync(CallerRole, id, request.Name, cancellationToken);
        return Ok(ToResponse(machine));
    }

    [HttpDelete("api/master-data/machines/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await useCase.DeleteAsync(CallerRole, id, cancellationToken);
        return NoContent();
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static MachineResponse ToResponse(Machine machine) => new(machine.Id, machine.Name, machine.LineId);
}
