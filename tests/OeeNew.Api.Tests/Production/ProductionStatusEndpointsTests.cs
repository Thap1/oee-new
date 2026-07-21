using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OeeNew.Api.Controllers;
using OeeNew.Api.Tests.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Production;

public class ProductionStatusEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    // MachineStatus serializes as a string on the server (Program.cs JsonStringEnumConverter); the
    // test's plain HttpClient needs the same converter to deserialize MachineStatusResponse back.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

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
    public async Task ListMachineStates_ScopedCaller_ReturnsOnlyInScopeMachines()
    {
        var adminClient = AdminClient();
        var (siteA, lineA, machineA) = await CreateSiteLineMachineAsync(adminClient);
        var (_, _, machineB) = await CreateSiteLineMachineAsync(adminClient);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [siteA.Id], [lineA.Id]));

        var response = await scopedClient.GetFromJsonAsync<List<MachineStatusResponse>>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        Assert.Contains(response!, m => m.MachineId == machineA.Id);
        Assert.DoesNotContain(response!, m => m.MachineId == machineB.Id);
    }

    [Fact]
    public async Task ListMachineStates_NeverReportedMachine_HasNullStatus()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        var response = await adminClient.GetFromJsonAsync<List<MachineStatusResponse>>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        var snapshot = Assert.Single(response!, m => m.MachineId == machine.Id);
        Assert.Null(snapshot.Status);
        Assert.Null(snapshot.Counter);
        Assert.Null(snapshot.LastReportedAt);
    }

    [Fact]
    public async Task ListMachineStates_AfterIngest_ReflectsLatestReading()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 99,
            status = "Idle",
        });

        var response = await adminClient.GetFromJsonAsync<List<MachineStatusResponse>>("/api/production/machine-states", JsonOptions);

        var snapshot = Assert.Single(response!, m => m.MachineId == machine.Id);
        Assert.Equal(OeeNew.Domain.Production.MachineStatus.Idle, snapshot.Status);
        Assert.Equal(99, snapshot.Counter);
    }
}
