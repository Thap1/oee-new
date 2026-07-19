using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

public class LinesEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        return client;
    }

    private async Task<SiteResponse> CreateSiteAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;

    [Fact]
    public async Task Create_WithExistingSite_ReturnsLineWithGeneratedId()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var name = $"Line {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LineResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(site.Id, body.SiteId);
    }

    [Fact]
    public async Task Create_WithUnknownSite_ReturnsBadRequestParentNotFound()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{Guid.NewGuid()}/lines", new CreateLineRequest("Line A"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("PARENT_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var site = await CreateSiteAsync(adminClient);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest("Blocked Line"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithChildMachine_ReturnsConflictWithDependentNames()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
        var machineName = $"Machine {Guid.NewGuid():N}";
        await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest(machineName));

        var response = await client.DeleteAsync($"/api/master-data/lines/{line.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("HAS_DEPENDENTS", error!.Code);
    }

    [Fact]
    public async Task Rename_WithExistingLine_UpdatesName()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
        var newName = $"Renamed {Guid.NewGuid():N}";

        var response = await client.PutAsJsonAsync($"/api/master-data/lines/{line.Id}", new RenameLineRequest(newName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LineResponse>();
        Assert.Equal(newName, body!.Name);
    }
}
