using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application;

namespace OeeNew.Api.Controllers;

/// <summary>Exposes AppMode to the frontend before a JWT exists (Story 5.2) — same reasoning as the existing anonymous `/.well-known/jwks.json` endpoint. Carries no sensitive data.</summary>
[ApiController]
[Route("api/app-mode")]
[AllowAnonymous]
public sealed class AppModeController(AppModeInfo appMode) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get() => Ok(new { mode = appMode.Mode });
}
