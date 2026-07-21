using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Api.Controllers;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.Production;

public class QualityRejectsEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        return client;
    }

    private async Task<(SiteResponse Site, LineResponse Line, MachineResponse Machine)> CreateSiteLineMachineAsync(HttpClient client)
    {
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
        var machine = (await (await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;
        return (site, line, machine);
    }

    [Fact]
    public async Task Record_AsOperatorInScope_ReturnsNoContentAndPersists()
    {
        var adminClient = AdminClient();
        var (site, line, machine) = await CreateSiteLineMachineAsync(adminClient);
        var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Operator", [site.Id], [line.Id]));

        var response = await operatorClient.PostAsJsonAsync($"/api/production/machines/{machine.Id}/quality-rejects", new { quantity = 4 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var reject = db.QualityRejects.Single(q => q.MachineId == machine.Id);
        Assert.Equal(4, reject.Quantity);
    }

    [Fact]
    public async Task Record_AsManager_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);
        var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager"));

        var response = await managerClient.PostAsJsonAsync($"/api/production/machines/{machine.Id}/quality-rejects", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Record_OperatorScopedToDifferentLine_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var (site, _, machine) = await CreateSiteLineMachineAsync(adminClient);
        var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Operator", [site.Id], [Guid.NewGuid()]));

        var response = await operatorClient.PostAsJsonAsync($"/api/production/machines/{machine.Id}/quality-rejects", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Record_WithZeroQuantity_ReturnsValidationError()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        var response = await adminClient.PostAsJsonAsync($"/api/production/machines/{machine.Id}/quality-rejects", new { quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
