using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

public class MachinesEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        return client;
    }

    private async Task<LineResponse> CreateSiteAndLineAsync(HttpClient client)
    {
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        return (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
    }

    [Fact]
    public async Task Create_WithExistingLine_ReturnsMachineWithGeneratedId()
    {
        var client = AdminClient();
        var line = await CreateSiteAndLineAsync(client);
        var name = $"Machine {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MachineResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(line.Id, body.LineId);
    }

    [Fact]
    public async Task Create_WithUnknownLine_ReturnsBadRequestParentNotFound()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync($"/api/master-data/lines/{Guid.NewGuid()}/machines", new CreateMachineRequest("Machine A"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("PARENT_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var line = await CreateSiteAndLineAsync(adminClient);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Viewer"));

        var response = await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest("Blocked Machine"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithExistingMachine_ReturnsNoContent()
    {
        var client = AdminClient();
        var line = await CreateSiteAndLineAsync(client);
        var machine = (await (await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;

        var response = await client.DeleteAsync($"/api/master-data/machines/{machine.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
