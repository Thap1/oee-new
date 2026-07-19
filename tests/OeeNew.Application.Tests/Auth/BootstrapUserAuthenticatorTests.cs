using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class BootstrapUserAuthenticatorTests
{
    private static BootstrapUserAuthenticator CreateAuthenticator(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        var hasher = new PasswordHasher<BootstrapUserAuthenticator>();
        var options = new BootstrapAdminOptions
        {
            UserId = adminId,
            Username = "admin",
            PasswordHash = hasher.HashPassword(null!, "ChangeMe123!"),
        };
        return new BootstrapUserAuthenticator(Options.Create(options));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithCorrectPassword_ReturnsGlobalAdmin()
    {
        var authenticator = CreateAuthenticator(out var adminId);

        var result = await authenticator.ValidateCredentialsAsync("admin", "ChangeMe123!");

        Assert.NotNull(result);
        Assert.Equal(adminId, result!.UserId);
        Assert.Equal("Admin", result.Role);
        Assert.Empty(result.SiteIds);
        Assert.Empty(result.LineIds);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithWrongPassword_ReturnsNull()
    {
        var authenticator = CreateAuthenticator(out _);

        var result = await authenticator.ValidateCredentialsAsync("admin", "wrong-password");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithUnknownUsername_ReturnsNull()
    {
        var authenticator = CreateAuthenticator(out _);

        var result = await authenticator.ValidateCredentialsAsync("someone-else", "ChangeMe123!");

        Assert.Null(result);
    }
}
