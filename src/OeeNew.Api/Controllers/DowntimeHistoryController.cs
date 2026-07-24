using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Auth;
using OeeNew.Application.Production;

namespace OeeNew.Api.Controllers;

public sealed record DowntimeHistoryEntryResponse(
    Guid Id,
    Guid MachineId,
    string MachineName,
    Guid? ReasonCodeId,
    string? ReasonCodeName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationSeconds);

/// <summary>Downtime history list for the nav "Dừng máy" page. Any authenticated role may read (same as the Dashboard's live status).</summary>
[ApiController]
[Authorize]
public sealed class DowntimeHistoryController(DowntimeHistoryQueryUseCase useCase) : ControllerBase
{
    [HttpGet("api/production/downtime-history")]
    public async Task<ActionResult<IReadOnlyList<DowntimeHistoryEntryResponse>>> List(CancellationToken cancellationToken)
    {
        var entries = await useCase.ListAsync(User.GetCallerScope(), cancellationToken);
        return Ok(entries.Select(ToResponse).ToList());
    }

    private static DowntimeHistoryEntryResponse ToResponse(DowntimeHistoryEntry e) =>
        new(e.Id, e.MachineId, e.MachineName, e.ReasonCodeId, e.ReasonCodeName, e.StartedAt, e.EndedAt, e.DurationSeconds);
}
