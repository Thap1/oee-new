using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application.Auth;

namespace OeeNew.Api.Controllers;

public sealed record LoginRequest([Required] string Username, [Required] string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);

public sealed record CurrentUserResponse(string UserId, string Username, string Role, IReadOnlyList<string> SiteIds, IReadOnlyList<string> LineIds);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(LoginUseCase loginUseCase) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var issued = await loginUseCase.ExecuteAsync(request.Username, request.Password, cancellationToken);
        return Ok(new LoginResponse(issued.AccessToken, issued.ExpiresAtUtc));
    }

    /// <summary>Returns the caller's own claims — proves the JWT Bearer + envelope wiring end-to-end (AC1-3).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        var user = HttpContext.User;
        var response = new CurrentUserResponse(
            UserId: user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? string.Empty,
            Username: user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("unique_name") ?? string.Empty,
            Role: user.FindFirstValue(OeeClaimTypes.Role) ?? string.Empty,
            SiteIds: user.FindAll(OeeClaimTypes.SiteId).Select(c => c.Value).ToList(),
            LineIds: user.FindAll(OeeClaimTypes.LineId).Select(c => c.Value).ToList());
        return Ok(response);
    }
}
