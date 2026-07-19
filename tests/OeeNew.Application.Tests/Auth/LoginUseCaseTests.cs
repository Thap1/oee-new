using OeeNew.Application.Auth;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class LoginUseCaseTests
{
    private sealed class FakeUserAuthenticator(AuthenticatedUser? result) : IUserAuthenticator
    {
        public Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public AuthenticatedUser? LastUser { get; private set; }

        public IssuedToken CreateToken(AuthenticatedUser user)
        {
            LastUser = user;
            return new IssuedToken("fake-token", DateTimeOffset.UtcNow.AddHours(1));
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_IssuesToken()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "admin", "Admin", SiteIds: [], LineIds: []);
        var tokenService = new FakeJwtTokenService();
        var useCase = new LoginUseCase(new FakeUserAuthenticator(user), tokenService);

        var result = await useCase.ExecuteAsync("admin", "correct-password");

        Assert.Equal("fake-token", result.AccessToken);
        Assert.Equal(user, tokenService.LastUser);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCredentials_ThrowsInvalidCredentialsException()
    {
        var useCase = new LoginUseCase(new FakeUserAuthenticator(null), new FakeJwtTokenService());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => useCase.ExecuteAsync("admin", "wrong-password"));
    }
}
