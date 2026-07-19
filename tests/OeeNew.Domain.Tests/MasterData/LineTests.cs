using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.MasterData;

public class LineTests
{
    [Fact]
    public void Constructor_WithValidSiteId_SetsProperties()
    {
        var siteId = Guid.NewGuid();

        var line = new Line(Guid.Empty, "Line A", siteId);

        Assert.Equal("Line A", line.Name);
        Assert.Equal(siteId, line.SiteId);
    }

    [Fact]
    public void Constructor_WithEmptySiteId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Line(Guid.Empty, "Line A", Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<MasterDataValidationException>(() => new Line(Guid.Empty, name, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithNameOverMaxLength_Throws()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<MasterDataValidationException>(() => new Line(Guid.Empty, tooLong, Guid.NewGuid()));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var line = new Line(Guid.Empty, "Line A", Guid.NewGuid());

        line.Rename("Line B");

        Assert.Equal("Line B", line.Name);
    }
}
