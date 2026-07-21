using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.Production;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.Production;

public class ProductionReadingsEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
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
    public async Task Ingest_AsOperator_ReturnsNoContentAndPersistsMachineState()
    {
        var adminClient = AdminClient();
        var (site, line, machine) = await CreateSiteLineMachineAsync(adminClient);

        var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Operator", [site.Id], [line.Id]));

        var response = await operatorClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 42,
            status = "Running",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var state = await db.MachineStates.FindAsync(machine.Id);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Running, state!.Status);
        Assert.Equal(42, state.Counter);
    }

    [Fact]
    public async Task Ingest_WithInvalidStatusString_ReturnsValidationError()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        var payload = $$"""
            { "machineId": "{{machine.Id}}", "timestamp": "{{DateTimeOffset.UtcNow:O}}", "counter": 1, "status": "NotARealStatus" }
            """;
        var response = await adminClient.PostAsync("/api/production/readings", new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    [Fact]
    public async Task Ingest_AsManager_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager"));

        var response = await managerClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Running",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_UnknownMachine_ReturnsParentNotFound()
    {
        var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));

        var response = await operatorClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = Guid.NewGuid(),
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Running",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("PARENT_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Ingest_CalledTwiceByDifferentCallers_BothLandInSameMachineState()
    {
        // Stand-in for AC #4: an "automatic" reading and a "manual entry" reading are just two
        // different callers hitting the exact same endpoint/use case — there is no second code path.
        var adminClient = AdminClient();
        var (site, line, machine) = await CreateSiteLineMachineAsync(adminClient);
        var baseTime = DateTimeOffset.UtcNow;

        var automaticCaller = factory.CreateClient();
        automaticCaller.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Operator", [site.Id], [line.Id]));
        var manualCaller = factory.CreateClient();
        manualCaller.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));

        await automaticCaller.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = baseTime,
            counter = 10,
            status = "Running",
        });
        await manualCaller.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = baseTime.AddSeconds(5),
            counter = 20,
            status = "Stopped",
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var state = await db.MachineStates.FindAsync(machine.Id);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Stopped, state!.Status);
        Assert.Equal(20, state.Counter);
    }
}
