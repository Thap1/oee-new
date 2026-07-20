using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

/// <summary>
/// Story 1.6, AC #4 — server-side scope enforcement for reads, verified by bypassing the UI entirely
/// and calling the API directly with a JWT scoped to a *different* Site than the one requested.
/// </summary>
public class ScopeEnforcementEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private async Task<Guid> CreateSiteAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        var name = $"Site {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(name));
        return (await response.Content.ReadFromJsonAsync<SiteResponse>())!.Id;
    }

    [Fact]
    public async Task ListSites_ScopedToOneSite_ExcludesOtherSites()
    {
        var siteA = await CreateSiteAsync();
        await CreateSiteAsync(); // siteB, out of scope for the token below

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager", [siteA], []));

        var response = await client.GetAsync("/api/master-data/sites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sites = await response.Content.ReadFromJsonAsync<List<SiteResponse>>();
        Assert.Single(sites!);
        Assert.Equal(siteA, sites![0].Id);
    }

    [Fact]
    public async Task ListLines_ForSiteOutsideJwtScope_ReturnsForbidden_EvenThoughUiWouldNeverSendIt()
    {
        // Simulates bypassing the UI selector entirely: caller's JWT only grants siteA, but the
        // request directly asks for siteB's lines via the URL path parameter.
        var siteA = await CreateSiteAsync();
        var siteB = await CreateSiteAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager", [siteA], []));

        var response = await client.GetAsync($"/api/master-data/sites/{siteB}/lines");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListLines_ForSiteInsideJwtScope_Succeeds()
    {
        var siteA = await CreateSiteAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager", [siteA], []));

        var response = await client.GetAsync($"/api/master-data/sites/{siteA}/lines");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListReasonCodes_ForSiteOutsideJwtScope_ReturnsForbidden()
    {
        var siteA = await CreateSiteAsync();
        var siteB = await CreateSiteAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Viewer", [siteA], []));

        var response = await client.GetAsync($"/api/master-data/sites/{siteB}/reason-codes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListSites_AsAdmin_IsUnrestrictedRegardlessOfEmptySiteIdsClaim()
    {
        await CreateSiteAsync();
        await CreateSiteAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));

        var response = await client.GetAsync("/api/master-data/sites");

        var sites = await response.Content.ReadFromJsonAsync<List<SiteResponse>>();
        Assert.True(sites!.Count >= 2);
    }
}
