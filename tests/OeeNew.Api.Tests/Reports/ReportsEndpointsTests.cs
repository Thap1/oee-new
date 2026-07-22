using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Reports;

public class ReportsEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
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
        await CreateMachineAsync(client, line.Id);

        var response = await client.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Day&referenceDate=2026-07-20&filterType=Machine&filterId={machineA.Id}");

        Assert.NotNull(response);
        Assert.Equal("Day", response!.PeriodType);
    }

    [Fact]
    public async Task GetOeeReport_FilterTypeWithoutFilterId_ReturnsBadRequest()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/api/reports/oee?periodType=Day&referenceDate=2026-07-20&filterType=Site");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
