using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("login")]
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
        // MapInboundClaims = false (Program.cs) means the JWT's own claim names ("sub",
        // "unique_name") are preserved as-is — no legacy ClaimTypes.* remapping ever populates them.
        var response = new CurrentUserResponse(
            UserId: user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
            Username: user.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty,
            Role: user.FindFirstValue(OeeClaimTypes.Role) ?? string.Empty,
            SiteIds: user.FindAll(OeeClaimTypes.SiteId).Select(c => c.Value).ToList(),
            LineIds: user.FindAll(OeeClaimTypes.LineId).Select(c => c.Value).ToList());
        return Ok(response);
    }
}
