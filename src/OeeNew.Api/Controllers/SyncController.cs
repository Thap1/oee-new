using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Api.Sync;
using OeeNew.Application;
using OeeNew.Application.Sync;

namespace OeeNew.Api.Controllers;

/// <summary>
/// Central-side receive endpoint for the Site sync push (Story 5.1). No JWT on this endpoint at all —
/// <see cref="ApiKeyAuthFilter"/> is the only gate, since a Site's push loop is a headless
/// machine-to-machine call, not a human/JWT-authenticated request.
/// </summary>
[ApiController]
[Route("api/sync")]
[AllowAnonymous]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public sealed class SyncController(ReceiveSyncBatchUseCase useCase, AppModeInfo appMode) : ControllerBase
{
    [HttpPost("batch")]
    public async Task<IActionResult> ReceiveBatch([FromBody] SyncBatch batch, CancellationToken cancellationToken)
    {
        if (!appMode.IsCentral)
        {
            // This instance isn't a Central instance — never silently accept a payload it has no reason to store.
            return NotFound();
        }

        await useCase.IngestAsync(batch, cancellationToken);
        return NoContent();
    }
}
