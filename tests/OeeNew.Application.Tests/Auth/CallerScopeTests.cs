using OeeNew.Application.Auth;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class CallerScopeTests
{
    private static readonly Guid SiteA = Guid.NewGuid();
    private static readonly Guid SiteB = Guid.NewGuid();
    private static readonly Guid LineA1 = Guid.NewGuid();
    private static readonly Guid LineA2 = Guid.NewGuid();

    [Fact]
    public void Global_AllowsAnySiteAndLine()
    {
        Assert.True(CallerScope.Global.AllowsSite(SiteA));
        Assert.True(CallerScope.Global.AllowsSite(SiteB));
        Assert.True(CallerScope.Global.AllowsLine(LineA1));
    }

    [Fact]
    public void Scoped_AllowsOnlyAssignedSites()
    {
        var scope = new CallerScope(false, [SiteA], []);

        Assert.True(scope.AllowsSite(SiteA));
        Assert.False(scope.AllowsSite(SiteB));
    }

    [Fact]
    public void Scoped_WithNoLineRestriction_AllowsAnyLine()
    {
        var scope = new CallerScope(false, [SiteA], []);

        Assert.True(scope.AllowsLine(LineA1));
        Assert.True(scope.AllowsLine(LineA2));
    }

    [Fact]
    public void Scoped_WithLineRestriction_AllowsOnlyAssignedLines()
    {
        var scope = new CallerScope(false, [SiteA], [LineA1]);

        Assert.True(scope.AllowsLine(LineA1));
        Assert.False(scope.AllowsLine(LineA2));
    }
}
