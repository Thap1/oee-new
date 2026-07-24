using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OeeNew.Api.Auth;
using OeeNew.Application.Auth;
using OeeNew.Application.Sync;
using OeeNew.Infrastructure.Sync;

namespace OeeNew.Api.Controllers;

public sealed record SiteSyncStatusResponse(Guid SiteId, string SiteName, DateTimeOffset? LastSyncedAt, bool IsStale);

/// <summary>
/// Human-facing, JWT-authenticated sync status read endpoint (Story 5.3) — deliberately a separate
/// controller from Story 5.1's <see cref="SyncController"/>, whose class-level <c>[AllowAnonymous]</c>
/// (for the machine-to-machine batch receive action) would otherwise risk silently applying to this
/// action too. No AppMode gate: at a Site instance <c>SiteSyncStatus</c> is always empty by construction
/// (Story 5.1), so this just returns every accessible site as "never synced" — harmless, and the
/// frontend only renders this panel at Central anyway.
/// </summary>
[ApiController]
[Route("api/sync/status")]
[Authorize(Policy = "ReportsAccess")]
public sealed class SyncStatusController(SyncStatusQueryUseCase useCase, IOptions<SyncOptions> syncOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SiteSyncStatusResponse>>> Get(CancellationToken cancellationToken)
    {
        var statuses = await useCase.GetStatusesAsync(User.GetCallerScope(), syncOptions.Value.WarningThresholdMinutes, cancellationToken);
        return Ok(statuses.Select(s => new SiteSyncStatusResponse(s.SiteId, s.SiteName, s.LastSyncedAt, s.IsStale)).ToList());
    }
}
