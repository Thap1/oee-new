using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Domain.MasterData;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

/// <summary>Story 5.2, Task 5: `SiteResponse.OpenAtUrl` is populated only at a Central instance, only for a Site.Id present in `Central:SiteLinks`, and stays null at a Site instance regardless of that config.</summary>
public class SitesOpenAtUrlEndpointsTests
{
    private static async Task SeedSitesAsync(SiteOpenAtUrlApiFactory factory, Guid unlinkedSiteId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();

        if (await db.Sites.FindAsync(SiteOpenAtUrlApiFactory.LinkedSiteId) is null)
        {
            db.Sites.Add(new Site(SiteOpenAtUrlApiFactory.LinkedSiteId, $"Linked Site {Guid.NewGuid():N}"));
        }

        db.Sites.Add(new Site(unlinkedSiteId, $"Unlinked Site {Guid.NewGuid():N}"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_AtCentral_PopulatesOpenAtUrlOnlyForConfiguredSite()
    {
        await using var factory = new SiteOpenAtUrlApiFactory { AppMode = "Central" };
        var unlinkedSiteId = Guid.NewGuid();
        await SeedSitesAsync(factory, unlinkedSiteId);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));

        var sites = await client.GetFromJsonAsync<List<SitesController_SiteResponse>>("/api/master-data/sites");

        var linked = sites!.Single(s => s.Id == SiteOpenAtUrlApiFactory.LinkedSiteId);
        var unlinked = sites!.Single(s => s.Id == unlinkedSiteId);
        Assert.Equal(SiteOpenAtUrlApiFactory.LinkedSiteUrl, linked.OpenAtUrl);
        Assert.Null(unlinked.OpenAtUrl);
    }

    [Fact]
    public async Task List_AtSite_OpenAtUrlIsNullEvenWithSiteLinksConfigured()
    {
        await using var factory = new SiteOpenAtUrlApiFactory { AppMode = "Site" };
        var unlinkedSiteId = Guid.NewGuid();
        await SeedSitesAsync(factory, unlinkedSiteId);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));

        var sites = await client.GetFromJsonAsync<List<SitesController_SiteResponse>>("/api/master-data/sites");

        var linked = sites!.Single(s => s.Id == SiteOpenAtUrlApiFactory.LinkedSiteId);
        Assert.Null(linked.OpenAtUrl);
    }

    // Mirrors OeeNew.Api.Controllers.SiteResponse's JSON shape for deserialization in this test file.
    private sealed record SitesController_SiteResponse(Guid Id, string Name, string? OpenAtUrl);
}
