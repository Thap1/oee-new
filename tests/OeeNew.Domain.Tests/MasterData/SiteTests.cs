using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.MasterData;

public class SiteTests
{
    [Fact]
    public void Constructor_WithValidName_SetsName()
    {
        var site = new Site(Guid.Empty, "Site A");

        Assert.Equal("Site A", site.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<MasterDataValidationException>(() => new Site(Guid.Empty, name));
    }

    [Fact]
    public void Constructor_WithNameOverMaxLength_Throws()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<MasterDataValidationException>(() => new Site(Guid.Empty, tooLong));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var site = new Site(Guid.Empty, "Site A");

        site.Rename("Site B");

        Assert.Equal("Site B", site.Name);
    }

    [Fact]
    public void Rename_WithBlankName_Throws()
    {
        var site = new Site(Guid.Empty, "Site A");

        Assert.Throws<MasterDataValidationException>(() => site.Rename(" "));
    }
}
