using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Reports;

public class ReportsEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
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

    private static async Task<SiteResponse> CreateSiteAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;

    private static async Task<LineResponse> CreateLineAsync(HttpClient client, Guid siteId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<LineResponse>())!;

    private static async Task<MachineResponse> CreateMachineAsync(HttpClient client, Guid lineId) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/lines/{lineId}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<MachineResponse>())!;

    private static async Task<ShiftScheduleResponse> CreateShiftAsync(HttpClient client, Guid siteId, Guid? lineId, TimeOnly start, TimeOnly end) =>
        (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/shift-schedules",
            new CreateShiftScheduleRequest($"Shift {Guid.NewGuid():N}", lineId, start, end)))
            .Content.ReadFromJsonAsync<ShiftScheduleResponse>())!;

    [Fact]
    public async Task GetOeeReport_OperatorRole_ReturnsForbidden()
    {
        var operatorClient = factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));

        var response = await operatorClient.GetAsync("/api/reports/oee?periodType=Day&referenceDate=2026-07-20");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Viewer")]
    [InlineData("Admin")]
    public async Task GetOeeReport_AllowedRoles_ReturnOk_ForASeededDayPeriod(string role)
    {
        var adminClient = AdminClient();
        var site = await CreateSiteAsync(adminClient);
        var line = await CreateLineAsync(adminClient, site.Id);
        var machine = await CreateMachineAsync(adminClient, line.Id);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor(role, [site.Id], [line.Id]));

        var response = await client.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Day&referenceDate=2026-07-20");

        Assert.NotNull(response);
        Assert.Equal("Day", response!.PeriodType);
        _ = machine;
    }

    [Fact]
    public async Task GetOeeReport_ShiftPeriod_ProducesExpectedWindowFromSeededShiftSchedule()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = await CreateLineAsync(client, site.Id);
        await CreateMachineAsync(client, line.Id);
        var shift = await CreateShiftAsync(client, site.Id, line.Id, new TimeOnly(8, 0), new TimeOnly(16, 0));

        var response = await client.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Shift&referenceDate=2026-07-20&shiftScheduleId={shift.Id}");

        Assert.NotNull(response);
        Assert.Equal("Shift", response!.PeriodType);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero), response.PeriodStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero), response.PeriodEnd);
    }

    [Fact]
    public async Task GetOeeReport_ShiftPeriodWithoutShiftScheduleId_ReturnsBadRequest()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/api/reports/oee?periodType=Shift&referenceDate=2026-07-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOeeReport_DayPeriodWithShiftScheduleId_ReturnsBadRequest()
    {
        var client = AdminClient();

        var response = await client.GetAsync($"/api/reports/oee?periodType=Day&referenceDate=2026-07-20&shiftScheduleId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOeeReport_SiteFilterOutsideCallerScope_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var siteA = await CreateSiteAsync(adminClient);
        var lineA = await CreateLineAsync(adminClient, siteA.Id);
        var siteB = await CreateSiteAsync(adminClient);

        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [siteA.Id], [lineA.Id]));

        var response = await scopedClient.GetAsync(
            $"/api/reports/oee?periodType=Day&referenceDate=2026-07-20&filterType=Site&filterId={siteB.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public async Task GetOeeReport_MachineFilter_ReturnsReportCountingOnlyThatMachine()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = await CreateLineAsync(client, site.Id);
        var machineA = await CreateMachineAsync(client, line.Id);
        var machineB = await CreateMachineAsync(client, line.Id);
        var reasonCode = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var t0 = DateTimeOffset.UtcNow;
        // Machine A: 20s of closed, reasoned downtime.
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machineA.Id, timestamp = t0, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machineA.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machineA.Id, timestamp = t0.AddSeconds(20), counter = 2, status = "Running" });
        // Machine B: 60s of closed, reasoned downtime — must NOT leak into the Machine-A-filtered report.
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machineB.Id, timestamp = t0, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machineB.Id}/downtime-reason", new { reasonCodeId = reasonCode.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machineB.Id, timestamp = t0.AddSeconds(60), counter = 2, status = "Running" });

        // Scoped client (Manager restricted to this Site/Line) — an unscoped Admin client would aggregate
        // every machine in the shared test database for "today", making the assertion below flaky.
        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [site.Id], [line.Id]));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await scopedClient.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Day&referenceDate={today:yyyy-MM-dd}&filterType=Machine&filterId={machineA.Id}");

        Assert.NotNull(response);
        Assert.Equal("Day", response!.PeriodType);
        Assert.Equal(20, response.AvailabilityLossSeconds);
    }

    [Fact]
    public async Task GetOeeReport_FilterTypeWithoutFilterId_ReturnsBadRequest()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/api/reports/oee?periodType=Day&referenceDate=2026-07-20&filterType=Site");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOeeReport_TwoReasonedDowntimeEvents_ReturnsTheHigherOneAsTopDowntimeReason_InOneResponse()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = await CreateLineAsync(client, site.Id);
        var machine = await CreateMachineAsync(client, line.Id);
        var smallReason = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Small {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;
        var bigReason = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{site.Id}/reason-codes",
            new CreateReasonCodeRequest($"Big {Guid.NewGuid():N}", LossCategory.PerformanceLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var t0 = DateTimeOffset.UtcNow;
        // First closed event, attributed to the small reason (20s).
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0, counter = 1, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = smallReason.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(20), counter = 2, status = "Running" });
        // Second closed event, attributed to the big reason (40s) — should win.
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(30), counter = 2, status = "Stopped" });
        await client.PostAsJsonAsync($"/api/production/machines/{machine.Id}/downtime-reason", new { reasonCodeId = bigReason.Id });
        await client.PostAsJsonAsync("/api/production/readings", new { machineId = machine.Id, timestamp = t0.AddSeconds(70), counter = 3, status = "Running" });

        // Scoped to just this test's Site/Line — Admin has global scope, and Day aggregates over every
        // machine in the shared test database, so an unscoped client here could pick up another test's
        // same-day downtime and flake on exactly which reason ranks first.
        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [site.Id], [line.Id]));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await scopedClient.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Day&referenceDate={today:yyyy-MM-dd}");

        Assert.NotNull(response);
        Assert.Equal(bigReason.Id, response!.TopDowntimeReasonCodeId);
        Assert.Equal(40, response.TopDowntimeReasonSeconds);
    }

    [Fact]
    public async Task GetOeeReport_NoDowntimeInPeriod_TopDowntimeReasonFieldsAllNull()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var line = await CreateLineAsync(client, site.Id);
        await CreateMachineAsync(client, line.Id);

        // Scoped client — an unscoped Admin client would aggregate every machine in the shared test
        // database for this date, which could pick up another test's downtime and flake this assertion.
        var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateTokenFor("Manager", [site.Id], [line.Id]));

        var response = await scopedClient.GetFromJsonAsync<OeeReportResponse>(
            "/api/reports/oee?periodType=Day&referenceDate=2026-07-20");

        Assert.NotNull(response);
        Assert.Null(response!.TopDowntimeReasonCodeId);
        Assert.Null(response.TopDowntimeReasonName);
        Assert.Null(response.TopDowntimeReasonSeconds);
    }

    [Fact]
    public async Task GetOeeReport_UndefinedPeriodTypeValue_ReturnsBadRequest_NotInternalError()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/api/reports/oee?periodType=99&referenceDate=2026-07-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    [Fact]
    public async Task GetOeeReport_UndefinedFilterTypeValue_ReturnsBadRequest_NotInternalError()
    {
        var client = AdminClient();

        var response = await client.GetAsync($"/api/reports/oee?periodType=Day&referenceDate=2026-07-20&filterType=99&filterId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }
}
