using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Analytics;

public class LossAnalyticsEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
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

    private static async Task<LineResponse> CreateLineAsync(HttpClient client, Guid siteId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;

    private static async Task<MachineResponse> CreateMachineAsync(HttpClient client, Guid lineId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/lines/{lineId}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;

    [Fact]
    public async Task ListAreas_OnlyReturnsLinesWithAMachineInCallerScope()
    {
        var adminClient = AdminClient();
        var site = (await (await adminClient.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var lineA = await CreateLineAsync(adminClient, site.Id);
        var lineB = await CreateLineAsync(adminClient, site.Id);
        await CreateMachineAsync(adminClient, lineA.Id);
        await CreateMachineAsync(adminClient, lineB.Id);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [site.Id], [lineA.Id]));

        var areas = await scopedClient.GetFromJsonAsync<List<LossAreaOptionResponse>>("/api/analytics/loss-areas");

        Assert.NotNull(areas);
        Assert.Contains(areas!, a => a.LineId == lineA.Id);
        Assert.DoesNotContain(areas, a => a.LineId == lineB.Id);
    }

    [Fact]
    public async Task GetBreakdown_TargetOutsideCallerScope_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var site = (await (await adminClient.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(adminClient, site.Id);
        var machine = await CreateMachineAsync(adminClient, line.Id);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [Guid.NewGuid()], [Guid.NewGuid()]));

        var response = await scopedClient.GetAsync($"/api/analytics/loss-breakdown?targetType=Equipment&targetId={machine.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public async Task GetBreakdown_NonexistentTargetId_ReturnsNotFound()
    {
        var client = AdminClient();

        var response = await client.GetAsync($"/api/analytics/loss-breakdown?targetType=Equipment&targetId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBreakdown_FullFlow_ClosedAttributedDowntime_CountsTowardsItsLossCategory()
    {
        var client = AdminClient();
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(client, site.Id);
        var machine = await CreateMachineAsync(client, line.Id);
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var startedAt = DateTimeOffset.UtcNow;
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = startedAt, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        await client.PostAsJsonAsync("/api/production/readings", new
        {
            machineId = machine.Id,
            timestamp = startedAt.AddSeconds(30),
            counter = 2,
            status = "Running",
        });

        var response = await client.GetFromJsonAsync<LossBreakdownResponse>(
            $"/api/analytics/loss-breakdown?targetType=Equipment&targetId={machine.Id}");

        Assert.NotNull(response);
        Assert.True(response!.AvailabilitySeconds >= 30);
        Assert.Equal(0, response.PerformanceSeconds);
        Assert.Equal(0, response.QualitySeconds);
    }

    [Fact]
    public async Task GetBreakdown_OpenEvent_IsExcludedEntirely()
    {
        // Story 3.1 Task 4 requires this exact scenario: an event that is Stopped and reasoned but
        // never resumed (still open, EndedAt == null) must not count towards any loss category.
        var client = AdminClient();
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(client, site.Id);
        var machine = await CreateMachineAsync(client, line.Id);
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = DateTimeOffset.UtcNow, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        // Deliberately no subsequent "Running" reading — the event stays open.

        var response = await client.GetFromJsonAsync<LossBreakdownResponse>(
            $"/api/analytics/loss-breakdown?targetType=Equipment&targetId={machine.Id}");

        Assert.NotNull(response);
        Assert.Equal(0, response!.AvailabilitySeconds);
        Assert.Equal(0, response.UnattributedSeconds);
    }

    [Fact]
    public async Task GetReasonBreakdown_TargetOutsideCallerScope_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var site = (await (await adminClient.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(adminClient, site.Id);
        var machine = await CreateMachineAsync(adminClient, line.Id);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [Guid.NewGuid()], [Guid.NewGuid()]));

        var response = await scopedClient.GetAsync(
            $"/api/analytics/loss-breakdown/reasons?targetType=Equipment&targetId={machine.Id}&lossCategory=AvailabilityLoss");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public async Task GetBreakdown_WithDate_OnlyCountsThatCalendarDay()
    {
        var client = AdminClient();
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(client, site.Id);
        var machine = await CreateMachineAsync(client, line.Id);
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        // An event started yesterday (UTC) — should not count towards today's requested date.
        var yesterday = DateTimeOffset.UtcNow.Date.AddDays(-1);
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = yesterday, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = yesterday.AddHours(1), counter = 2, status = "Running" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.GetFromJsonAsync<LossBreakdownResponse>(
            $"/api/analytics/loss-breakdown?targetType=Equipment&targetId={machine.Id}&date={today:yyyy-MM-dd}");

        Assert.NotNull(response);
        Assert.Equal(0, response!.AvailabilitySeconds);
    }

    [Fact]
    public async Task GetReasonBreakdown_FullFlow_GroupsAndSumsByReasonCodeWithinTheRequestedCategory()
    {
        var client = AdminClient();
        var site = (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;
        var line = await CreateLineAsync(client, site.Id);
        var machine = await CreateMachineAsync(client, line.Id);
        var reasonA = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason A {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;
        var reasonB = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason B {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var t0 = DateTimeOffset.UtcNow;
        // First closed event, attributed to Reason A.
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonA.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(20), counter = 2, status = "Running" });
        // Second closed event, attributed to Reason B.
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(30), counter = 2, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = reasonB.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(40), counter = 3, status = "Running" });

        var items = await client.GetFromJsonAsync<List<ReasonBreakdownItemResponse>>(
            $"/api/analytics/loss-breakdown/reasons?targetType=Equipment&targetId={machine.Id}&lossCategory=AvailabilityLoss");

        Assert.NotNull(items);
        Assert.Contains(items!, i => i.ReasonCodeId == reasonA.Id && i.DurationSeconds >= 20);
        Assert.Contains(items!, i => i.ReasonCodeId == reasonB.Id && i.DurationSeconds >= 10);
    }
}
