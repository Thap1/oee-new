using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;
using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Identity;

public class CompositeUserAuthenticatorTests
{
    private static readonly IPasswordHasher<User> UserHasher = new PasswordHasher<User>();
    private static readonly IPasswordHasher<BootstrapUserAuthenticator> BootstrapHasher = new PasswordHasher<BootstrapUserAuthenticator>();

    private static BootstrapUserAuthenticator CreateBootstrap(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        var options = new BootstrapAdminOptions
        {
            UserId = adminId,
            Username = "admin",
            PasswordHash = BootstrapHasher.HashPassword(null!, "ChangeMe123!"),
        };
        return new BootstrapUserAuthenticator(Options.Create(options));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UserExistsInPersistedStore_ReturnsPersistedResult()
    {
        var repo = new FakeUserRepository();
        repo.Seed("mgr1", UserRole.Manager, UserHasher.HashPassword(null!, "Passw0rd!"), [Guid.NewGuid()], []);
        var composite = new CompositeUserAuthenticator(new PersistedUserAuthenticator(repo), CreateBootstrap(out _), repo);

        var result = await composite.ValidateCredentialsAsync("mgr1", "Passw0rd!");

        Assert.NotNull(result);
        Assert.Equal("Manager", result!.Role);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UserNotInPersistedStore_FallsBackToBootstrapAdmin()
    {
        var repo = new FakeUserRepository();
        var composite = new CompositeUserAuthenticator(new PersistedUserAuthenticator(repo), CreateBootstrap(out var adminId), repo);

        var result = await composite.ValidateCredentialsAsync("admin", "ChangeMe123!");

        Assert.NotNull(result);
        Assert.Equal(adminId, result!.UserId);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_PersistedStoreThrows_FallsBackToBootstrapAdmin()
    {
        var throwingRepo = new ThrowingUserRepository();
        var throwingAuthenticator = new PersistedUserAuthenticator(throwingRepo);
        var composite = new CompositeUserAuthenticator(throwingAuthenticator, CreateBootstrap(out var adminId), throwingRepo);

        var result = await composite.ValidateCredentialsAsync("admin", "ChangeMe123!");

        Assert.NotNull(result);
        Assert.Equal(adminId, result!.UserId);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_PersistedUserExistsWithWrongPassword_DoesNotFallBackToBootstrapAdmin()
    {
        // Story 1.4 review: a persisted user whose username collides with the bootstrap Admin's
        // configured username must not be authenticatable via the bootstrap password.
        var repo = new FakeUserRepository();
        repo.Seed("admin", UserRole.Admin, UserHasher.HashPassword(null!, "RealPassword1!"), [], []);
        var composite = new CompositeUserAuthenticator(new PersistedUserAuthenticator(repo), CreateBootstrap(out _), repo);

        var result = await composite.ValidateCredentialsAsync("admin", "ChangeMe123!");

        Assert.Null(result);
    }

    private sealed class ThrowingUserRepository : IUserRepository
    {
        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB unreachable");
        public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB unreachable");
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB unreachable");
        public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB unreachable");
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB unreachable");
    }
}
