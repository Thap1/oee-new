using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.Identity;
using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Identity;

public class PersistedUserAuthenticatorTests
{
    private static readonly IPasswordHasher<User> Hasher = new PasswordHasher<User>();

    [Fact]
    public async Task ValidateCredentialsAsync_WithCorrectPassword_ReturnsUserWithScope()
    {
        var repo = new FakeUserRepository();
        var authenticator = new PersistedUserAuthenticator(repo);
        var siteId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var hash = Hasher.HashPassword(null!, "Passw0rd!");
        repo.Seed("op1", UserRole.Operator, hash, [siteId], [lineId]);

        var result = await authenticator.ValidateCredentialsAsync("op1", "Passw0rd!");

        Assert.NotNull(result);
        Assert.Equal("Operator", result!.Role);
        Assert.Equal([siteId], result.SiteIds);
        Assert.Equal([lineId], result.LineIds);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithWrongPassword_ReturnsNull()
    {
        var repo = new FakeUserRepository();
        var authenticator = new PersistedUserAuthenticator(repo);
        var hash = Hasher.HashPassword(null!, "Passw0rd!");
        repo.Seed("op1", UserRole.Operator, hash, [Guid.NewGuid()], [Guid.NewGuid()]);

        var result = await authenticator.ValidateCredentialsAsync("op1", "wrong");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithUnknownUsername_ReturnsNull()
    {
        var authenticator = new PersistedUserAuthenticator(new FakeUserRepository());

        var result = await authenticator.ValidateCredentialsAsync("nobody", "Passw0rd!");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_DeactivatedUser_ReturnsNullEvenWithCorrectPassword()
    {
        var repo = new FakeUserRepository();
        var authenticator = new PersistedUserAuthenticator(repo);
        var hash = Hasher.HashPassword(null!, "Passw0rd!");
        var userId = repo.Seed("op1", UserRole.Operator, hash, [Guid.NewGuid()], [Guid.NewGuid()]);
        var user = await repo.GetAsync(userId);
        user!.Deactivate();
        await repo.UpdateAsync(user);

        var result = await authenticator.ValidateCredentialsAsync("op1", "Passw0rd!");

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_ForOperatorCreatedViaUseCase_IssuesJwtWithMatchingSiteAndLineClaims()
    {
        // AC #1 end-to-end (simulated login, no HTTP): create an Operator via UserManagementUseCase,
        // then log in through PersistedUserAuthenticator + JwtTokenService and decode the resulting
        // JWT to prove its site_id/line_id claims match what was assigned at creation.
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var userRepo = new FakeUserRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new UserManagementUseCase(userRepo, siteRepo, lineRepo, new CentralCredentialProvisioner());
        await useCase.CreateAsync("Admin", "op1", "Passw0rd!", UserRole.Operator, [siteId], [lineId]);

        var authenticator = new PersistedUserAuthenticator(userRepo);
        var authenticated = await authenticator.ValidateCredentialsAsync("op1", "Passw0rd!");
        Assert.NotNull(authenticated);

        var provider = new RsaJwtSigningKeyProvider();
        try
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenLifetimeMinutes = 60,
            });
            var tokenService = new JwtTokenService(provider, jwtOptions);
            var issued = tokenService.CreateToken(authenticated!);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

            Assert.Equal("Operator", jwt.Claims.Single(c => c.Type == OeeClaimTypes.Role).Value);
            Assert.Equal([siteId.ToString()], jwt.Claims.Where(c => c.Type == OeeClaimTypes.SiteId).Select(c => c.Value));
            Assert.Equal([lineId.ToString()], jwt.Claims.Where(c => c.Type == OeeClaimTypes.LineId).Select(c => c.Value));
        }
        finally
        {
            provider.Dispose();
        }
    }
}
