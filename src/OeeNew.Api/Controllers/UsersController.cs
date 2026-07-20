using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Api.Controllers;

public sealed record CreateUserRequest([Required] string Username, [Required] string Password, UserRole Role, Guid[]? SiteIds, Guid[]? LineIds);
public sealed record UpdateUserRoleRequest(UserRole Role, Guid[]? SiteIds, Guid[]? LineIds);
public sealed record UserResponse(Guid Id, string Username, UserRole Role, Guid[] SiteIds, Guid[] LineIds);

/// <summary>User management CRUD (Story 1.4, FR-013). Admin only, throughout (AC #4, NFR-5).</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]
public sealed class UsersController(UserManagementUseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        var users = await useCase.ListAsync(CallerRole, cancellationToken);
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await useCase.CreateAsync(
            CallerRole, request.Username, request.Password, request.Role,
            request.SiteIds ?? [], request.LineIds ?? [], cancellationToken);
        return CreatedAtAction(nameof(List), null, ToResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> UpdateRoleAndScope(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await useCase.UpdateRoleAndScopeAsync(
            CallerRole, id, request.Role, request.SiteIds ?? [], request.LineIds ?? [], cancellationToken);
        return Ok(ToResponse(user));
    }

    private string? CallerRole => User.FindFirstValue(OeeClaimTypes.Role);

    private static UserResponse ToResponse(User user) => new(user.Id, user.Username, user.Role, user.SiteIds, user.LineIds);
}
