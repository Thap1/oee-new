using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

public class SitesEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        return client;
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsSiteWithGeneratedId()
    {
        var client = AdminClient();
        var name = $"Site {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SiteResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(name, body.Name);
    }

    [Fact]
    public async Task Create_ThenList_IncludesNewSite()
    {
        var client = AdminClient();
        var name = $"Site {Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(name));

        var response = await client.GetAsync("/api/master-data/sites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sites = await response.Content.ReadFromJsonAsync<List<SiteResponse>>();
        Assert.Contains(sites!, s => s.Name == name);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager"));

        var response = await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest("Blocked Site"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public async Task Delete_WithChildLine_ReturnsConflictWithDependentNames()
    {
        var client = AdminClient();
        var siteName = $"Site {Guid.NewGuid():N}";
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(siteName)))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var lineName = $"Line {Guid.NewGuid():N}";
        await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest(lineName));

        var response = await client.DeleteAsync($"/api/master-data/sites/{site.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("HAS_DEPENDENTS", error!.Code);
    }

    [Fact]
    public async Task Delete_WithoutChildLines_ReturnsNoContent()
    {
        var client = AdminClient();
        var siteName = $"Site {Guid.NewGuid():N}";
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(siteName)))
            .Content.ReadFromJsonAsync<SiteResponse>())!;

        var response = await client.DeleteAsync($"/api/master-data/sites/{site.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
