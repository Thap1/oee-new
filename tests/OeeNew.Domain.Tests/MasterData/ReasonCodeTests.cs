using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.MasterData;

public class ReasonCodeTests
{
    private static readonly Guid SiteId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidData_IsActiveByDefault()
    {
        var reasonCode = new ReasonCode(Guid.Empty, SiteId, "Changeover", LossCategory.PerformanceLoss);

        Assert.Equal("Changeover", reasonCode.Name);
        Assert.Equal(LossCategory.PerformanceLoss, reasonCode.LossCategory);
        Assert.True(reasonCode.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<MasterDataValidationException>(() => new ReasonCode(Guid.Empty, SiteId, name, LossCategory.QualityLoss));
    }

    [Fact]
    public void Constructor_WithNameOverMaxLength_Throws()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<MasterDataValidationException>(() => new ReasonCode(Guid.Empty, SiteId, tooLong, LossCategory.QualityLoss));
    }

    [Fact]
    public void Constructor_WithUndefinedLossCategory_Throws()
    {
        Assert.Throws<MasterDataValidationException>(() => new ReasonCode(Guid.Empty, SiteId, "Bad", (LossCategory)99));
    }

    [Fact]
    public void Constructor_WithEmptySiteId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReasonCode(Guid.Empty, Guid.Empty, "Changeover", LossCategory.AvailabilityLoss));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var reasonCode = new ReasonCode(Guid.Empty, SiteId, "Changeover", LossCategory.PerformanceLoss);

        reasonCode.Deactivate();

        Assert.False(reasonCode.IsActive);
    }
}
