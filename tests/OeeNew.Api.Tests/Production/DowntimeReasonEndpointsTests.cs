using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.Production;

public class DowntimeReasonEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
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

    private async Task<(Guid SiteId, MachineResponse Machine, ReasonCodeResponse ReasonCode)> CreateStoppedMachineWithReasonAsync(HttpClient client)
    {
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;
        var machine = (await (await client.PostAsJsonAsync($"/api/master-data/lines/{line.Id}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow,
            counter = 1,
            status = "Stopped",
        });

        return (site.Id, machine, reasonCode);
    }

    [Fact]
    public async Task AttachReason_FullFlow_StoppedMachineThenReason_ReturnsNoContent()
    {
        var client = AdminClient();
        var (_, machine, reasonCode) = await CreateStoppedMachineWithReasonAsync(client);

        var response = await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var downtimeEvent = db.DowntimeEvents.First(e => e.MachineId == machine.Id);
        Assert.Equal(reasonCode.Id, downtimeEvent.ReasonCodeId);
    }

    [Fact]
    public async Task AttachReason_MachineAlreadyRunningAgain_ReturnsNotFound()
    {
        var client = AdminClient();
        var (_, machine, reasonCode) = await CreateStoppedMachineWithReasonAsync(client);
        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = DateTimeOffset.UtcNow.AddSeconds(5),
            counter = 2,
            status = "Running",
        });

        var response = await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("DOWNTIME_EVENT_NOT_OPEN", error!.Code);
    }
}
