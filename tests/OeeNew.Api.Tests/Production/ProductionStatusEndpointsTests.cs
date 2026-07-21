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

    private static async Task<LineResponse> CreateLineAsync(HttpClient client, Guid siteId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;

    private static async Task<MachineResponse> CreateMachineAsync(HttpClient client, Guid lineId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/lines/{lineId}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;

    [Fact]
    public async Task ListMachineStates_ScopedCaller_ReturnsOnlyInScopeMachines()
    {
        var adminClient = AdminClient();
        var (siteA, lineA, machineA) = await CreateSiteLineMachineAsync(adminClient);
        var (_, _, machineB) = await CreateSiteLineMachineAsync(adminClient);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [siteA.Id], [lineA.Id]));

        var response = await scopedClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        Assert.Contains(response!.Machines, m => m.MachineId == machineA.Id);
        Assert.DoesNotContain(response.Machines, m => m.MachineId == machineB.Id);
    }

    [Fact]
    public async Task ListMachineStates_NeverReportedMachine_HasNullStatus()
    {
        var adminClient = AdminClient();
        var (_, _, machine) = await CreateSiteLineMachineAsync(adminClient);

        var response = await adminClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        var snapshot = Assert.Single(response!.Machines, m => m.MachineId == machine.Id);
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

        var response = await adminClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        var snapshot = Assert.Single(response!.Machines, m => m.MachineId == machine.Id);
        Assert.Equal(OeeNew.Domain.Production.MachineStatus.Idle, snapshot.Status);
        Assert.Equal(99, snapshot.Counter);
    }

    [Fact]
    public async Task ListMachineStates_ManagerScopedToTwoLines_SeesOnlyMachinesOnThoseLines()
    {
        // Story 2.4 AC #1: a Manager scoped to Line A and Line B (same Site) sees exactly the
        // machines on those two Lines, not a third Line's machines on the same Site.
        var adminClient = AdminClient();
        var site = (await (await adminClient.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var lineA = await CreateLineAsync(adminClient, site.Id);
        var lineB = await CreateLineAsync(adminClient, site.Id);
        var lineC = await CreateLineAsync(adminClient, site.Id);
        var machineA = await CreateMachineAsync(adminClient, lineA.Id);
        var machineB = await CreateMachineAsync(adminClient, lineB.Id);
        var machineC = await CreateMachineAsync(adminClient, lineC.Id);

        var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [site.Id], [lineA.Id, lineB.Id]));

        var response = await managerClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        Assert.Contains(response!.Machines, m => m.MachineId == machineA.Id);
        Assert.Contains(response.Machines, m => m.MachineId == machineB.Id);
        Assert.DoesNotContain(response.Machines, m => m.MachineId == machineC.Id);
    }

    [Fact]
    public async Task ListMachineStates_CallerScopedToLineWithNoMachines_ReturnsEmptyListNotAnError()
    {
        // Story 2.4 AC #3: there is no spoofable siteId/lineId parameter on this endpoint — a caller
        // scoped to a Line that isn't among the ones with Machines simply gets an empty list, not
        // an error and never another Line's data. This is the "filtering IS the enforcement" case.
        var adminClient = AdminClient();
        var (_, _, _) = await CreateSiteLineMachineAsync(adminClient);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [Guid.NewGuid()], [Guid.NewGuid()]));

        var response = await scopedClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        Assert.Empty(response!.Machines);
    }

    [Fact]
    public async Task ListMachineStates_ResponseIncludesConfiguredNoSignalThreshold()
    {
        var adminClient = AdminClient();

        var response = await adminClient.GetFromJsonAsync<MachineStatesResponse>("/api/production/machine-states", JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(60, response!.NoSignalThresholdSeconds);
    }
}
