using OeeNew.Domain.Identity;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.Identity;

public class UserTests
{
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid LineId = Guid.NewGuid();

    [Fact]
    public void Constructor_AdminWithNoScope_Succeeds()
    {
        var user = new User(Guid.Empty, "admin", UserRole.Admin, "hash", [], []);

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Empty(user.SiteIds);
        Assert.Empty(user.LineIds);
    }

    [Fact]
    public void Constructor_AdminWithSiteScope_Throws()
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, "admin", UserRole.Admin, "hash", [SiteId], []));
    }

    [Fact]
    public void Constructor_ManagerWithoutSite_Throws()
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [], []));
    }

    [Fact]
    public void Constructor_ManagerWithSiteOnly_Succeeds()
    {
        var user = new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [SiteId], []);

        Assert.Equal([SiteId], user.SiteIds);
        Assert.Empty(user.LineIds);
    }

    [Fact]
    public void Constructor_ViewerWithSiteOnly_Succeeds()
    {
        var user = new User(Guid.Empty, "viewer", UserRole.Viewer, "hash", [SiteId], []);

        Assert.Equal([SiteId], user.SiteIds);
    }

    [Fact]
    public void Constructor_OperatorWithSiteButNoLine_Throws()
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, "op", UserRole.Operator, "hash", [SiteId], []));
    }

    [Fact]
    public void Constructor_OperatorWithSiteAndLine_Succeeds()
    {
        var user = new User(Guid.Empty, "op", UserRole.Operator, "hash", [SiteId], [LineId]);

        Assert.Equal([SiteId], user.SiteIds);
        Assert.Equal([LineId], user.LineIds);
    }

    [Fact]
    public void Constructor_DuplicateSiteIds_Deduplicates()
    {
        var user = new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [SiteId, SiteId], []);

        Assert.Single(user.SiteIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankUsername_Throws(string username)
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, username, UserRole.Admin, "hash", [], []));
    }

    [Fact]
    public void Constructor_WithBlankPasswordHash_Throws()
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, "admin", UserRole.Admin, "", [], []));
    }

    [Fact]
    public void Rescope_FromOperatorToAdmin_ClearsScope()
    {
        var user = new User(Guid.Empty, "op", UserRole.Operator, "hash", [SiteId], [LineId]);

        user.Rescope(UserRole.Admin, [], []);

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Empty(user.SiteIds);
    }

    [Fact]
    public void Rescope_FromAdminToOperatorWithoutLine_Throws()
    {
        var user = new User(Guid.Empty, "admin", UserRole.Admin, "hash", [], []);

        Assert.Throws<MasterDataValidationException>(() => user.Rescope(UserRole.Operator, [SiteId], []));
    }

    [Fact]
    public void Constructor_ManagerWithLine_Throws()
    {
        Assert.Throws<MasterDataValidationException>(
            () => new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [SiteId], [LineId]));
    }

    [Fact]
    public void Constructor_NewUser_IsActive()
    {
        var user = new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [SiteId], []);

        Assert.True(user.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = new User(Guid.Empty, "mgr", UserRole.Manager, "hash", [SiteId], []);

        user.Deactivate();

        Assert.False(user.IsActive);
    }
}
