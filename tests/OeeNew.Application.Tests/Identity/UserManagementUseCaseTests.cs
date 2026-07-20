using OeeNew.Application.Identity;
using OeeNew.Application.MasterData;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Identity;

public class UserManagementUseCaseTests
{
    private static UserManagementUseCase CreateUseCase(
        out FakeSiteRepository siteRepo, out FakeLineRepository lineRepo, out FakeUserRepository userRepo,
        bool centralReachable = true)
    {
        siteRepo = new FakeSiteRepository();
        lineRepo = new FakeLineRepository();
        userRepo = new FakeUserRepository();
        return new UserManagementUseCase(userRepo, siteRepo, lineRepo, new FakeCentralCredentialProvisioner(centralReachable));
    }

    [Fact]
    public async Task CreateAsync_OperatorWithSiteAndLine_PersistsUserWithMatchingScope()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out _);
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);

        var user = await useCase.CreateAsync("Admin", "op1", "Passw0rd!", UserRole.Operator, [siteId], [lineId]);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal([siteId], user.SiteIds);
        Assert.Equal([lineId], user.LineIds);
        Assert.Equal("hashed:Passw0rd!", user.PasswordHash);
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out _);
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<MasterDataForbiddenException>(
            () => useCase.CreateAsync("Manager", "op1", "Passw0rd!", UserRole.Manager, [siteId], []));
    }

    [Fact]
    public async Task CreateAsync_WithUnknownSite_ThrowsParentNotFound()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(
            () => useCase.CreateAsync("Admin", "mgr1", "Passw0rd!", UserRole.Manager, [Guid.NewGuid()], []));
    }

    [Fact]
    public async Task CreateAsync_WithLineFromDifferentSite_ThrowsParentNotFound()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out _);
        var siteId = siteRepo.Seed("Site A");
        var otherSiteId = siteRepo.Seed("Site B");
        var lineUnderOtherSite = lineRepo.Seed("Line B", otherSiteId);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(
            () => useCase.CreateAsync("Admin", "op1", "Passw0rd!", UserRole.Operator, [siteId], [lineUnderOtherSite]));
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateUsername_ThrowsUsernameAlreadyTaken()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var userRepo);
        var siteId = siteRepo.Seed("Site A");
        userRepo.Seed("mgr1", UserRole.Manager, "existing-hash", [siteId], []);

        await Assert.ThrowsAsync<UsernameAlreadyTakenException>(
            () => useCase.CreateAsync("Admin", "mgr1", "Passw0rd!", UserRole.Manager, [siteId], []));
    }

    [Fact]
    public async Task CreateAsync_WhenCentralUnreachable_ThrowsCredentialProvisioningException_AndPersistsNoUser()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var userRepo, centralReachable: false);
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<CredentialProvisioningException>(
            () => useCase.CreateAsync("Admin", "mgr1", "Passw0rd!", UserRole.Manager, [siteId], []));

        Assert.Empty(await userRepo.ListAsync());
    }

    [Fact]
    public async Task UpdateRoleAndScopeAsync_ChangesRoleAndScope()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out var userRepo);
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var userId = userRepo.Seed("op1", UserRole.Operator, "hash", [siteId], [lineId]);

        var updated = await useCase.UpdateRoleAndScopeAsync("Admin", userId, UserRole.Manager, [siteId], []);

        Assert.Equal(UserRole.Manager, updated.Role);
        Assert.Empty(updated.LineIds);
    }

    [Fact]
    public async Task UpdateRoleAndScopeAsync_WithUnknownUser_ThrowsNotFound()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(
            () => useCase.UpdateRoleAndScopeAsync("Admin", Guid.NewGuid(), UserRole.Admin, [], []));
    }

    [Fact]
    public async Task UpdateRoleAndScopeAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var userRepo);
        var siteId = siteRepo.Seed("Site A");
        var userId = userRepo.Seed("mgr1", UserRole.Manager, "hash", [siteId], []);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(
            () => useCase.UpdateRoleAndScopeAsync("Viewer", userId, UserRole.Manager, [siteId], []));
    }

    [Fact]
    public async Task ListAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.ListAsync("Operator"));
    }
}
