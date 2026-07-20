using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

public class ReasonCodeManagementUseCaseTests
{
    private static ReasonCodeManagementUseCase CreateUseCase(out FakeSiteRepository siteRepo, out FakeReasonCodeRepository reasonCodeRepo)
    {
        siteRepo = new FakeSiteRepository();
        reasonCodeRepo = new FakeReasonCodeRepository();
        return new ReasonCodeManagementUseCase(reasonCodeRepo, siteRepo);
    }

    [Fact]
    public async Task CreateAsync_WithValidLossCategory_PersistsReasonCode()
    {
        var useCase = CreateUseCase(out var siteRepo, out _);
        var siteId = siteRepo.Seed("Site A");

        var reasonCode = await useCase.CreateAsync("Admin", siteId, "Changeover", LossCategory.PerformanceLoss);

        Assert.NotEqual(Guid.Empty, reasonCode.Id);
        Assert.Equal(LossCategory.PerformanceLoss, reasonCode.LossCategory);
        Assert.True(reasonCode.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WithNullLossCategory_ThrowsValidationError()
    {
        var useCase = CreateUseCase(out var siteRepo, out _);
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<MasterDataValidationException>(
            () => useCase.CreateAsync("Admin", siteId, "Changeover", null));
    }

    [Fact]
    public async Task CreateAsync_WithUnknownSite_ThrowsParentNotFound()
    {
        var useCase = CreateUseCase(out _, out _);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(
            () => useCase.CreateAsync("Admin", Guid.NewGuid(), "Changeover", LossCategory.QualityLoss));
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _);
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<MasterDataForbiddenException>(
            () => useCase.CreateAsync("Viewer", siteId, "Changeover", LossCategory.QualityLoss));
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse_WithoutDeletingRecord()
    {
        var useCase = CreateUseCase(out var siteRepo, out var reasonCodeRepo);
        var siteId = siteRepo.Seed("Site A");
        var id = reasonCodeRepo.Seed(siteId, "Changeover", LossCategory.PerformanceLoss);

        var deactivated = await useCase.DeactivateAsync("Admin", id);

        Assert.False(deactivated.IsActive);
        Assert.NotNull(await reasonCodeRepo.GetAsync(id));
    }

    [Fact]
    public async Task DeactivateAsync_WithUnknownReasonCode_ThrowsNotFound()
    {
        var useCase = CreateUseCase(out _, out _);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.DeactivateAsync("Admin", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeactivateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out var reasonCodeRepo);
        var siteId = siteRepo.Seed("Site A");
        var id = reasonCodeRepo.Seed(siteId, "Changeover", LossCategory.PerformanceLoss);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.DeactivateAsync("Operator", id));
    }
}
