using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OeeNew.Api.Controllers;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Production;

public class DowntimeHistoryEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
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

    private async Task<(Guid SiteId, MachineResponse Machine)> CreateMachineAsync(HttpClient client)
    {
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
        var machine = (await (await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;
        return (site.Id, machine);
    }

    [Fact]
    public async Task List_OpenDowntimeEventWithNoReason_ReturnsEntryWithNullEndedAtAndReason()
    {
        var client = AdminClient();
        var (_, machine) = await CreateMachineAsync(client);

        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Stopped",
        });

        var history = await client.GetFromJsonAsync<List<DowntimeHistoryEntryResponse>>("/api/production/downtime-history");

        Assert.NotNull(history);
        var entry = Assert.Single(history!, e => e.MachineId == machine.Id);
        Assert.Null(entry.ReasonCodeId);
        Assert.Null(entry.EndedAt);
        Assert.Null(entry.DurationSeconds);
    }

    [Fact]
    public async Task List_ClosedDowntimeEventWithReason_ReturnsResolvedReasonAndDuration()
    {
        var client = AdminClient();
        var (siteId, machine) = await CreateMachineAsync(client);
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Stopped",
        });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow.AddSeconds(5),
            counter = 2,
            status = "Running",
        });

        var history = await client.GetFromJsonAsync<List<DowntimeHistoryEntryResponse>>("/api/production/downtime-history");

        Assert.NotNull(history);
        var entry = Assert.Single(history!, e => e.MachineId == machine.Id);
        Assert.Equal(reasonCode.Id, entry.ReasonCodeId);
        Assert.Equal(reasonCode.Name, entry.ReasonCodeName);
        Assert.NotNull(entry.EndedAt);
        Assert.NotNull(entry.DurationSeconds);
    }

    [Fact]
    public async Task List_ScopedCaller_OnlySeesEventsForMachinesInScope()
    {
        var adminClient = AdminClient();
        var (siteA, machineA) = await CreateMachineAsync(adminClient);
        var (_, machineB) = await CreateMachineAsync(adminClient);
        await adminClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machineA.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Stopped",
        });
        await adminClient.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machineB.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Stopped",
        });

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager", [siteA], []));

        var history = await scopedClient.GetFromJsonAsync<List<DowntimeHistoryEntryResponse>>("/api/production/downtime-history");

        Assert.NotNull(history);
        Assert.Contains(history!, e => e.MachineId == machineA.Id);
        Assert.DoesNotContain(history!, e => e.MachineId == machineB.Id);
    }
}
