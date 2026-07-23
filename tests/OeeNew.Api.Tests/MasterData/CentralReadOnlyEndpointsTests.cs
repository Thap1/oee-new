using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.Sync;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

/// <summary>Story 5.2, AC #3: Site/Line/Machine/ShiftSchedule/ReasonCode writes are blocked with 403 CENTRAL_READ_ONLY at a Central-mode instance; the same requests still succeed at a Site-mode instance (regression).</summary>
public class CentralReadOnlyEndpointsTests(CentralSyncApiFactory centralFactory) : IClassFixture<CentralSyncApiFactory>
{
    private HttpClient CentralAdminClient()
    {
        var client = centralFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", centralFactory.CreateTokenFor("Admin"));
        return client;
    }

    [Fact]
    public async Task CreateSite_AtCentral_ReturnsCentralReadOnly()
    {
        var response = await CentralAdminClient().PostAsJsonAsync(
            "/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("CENTRAL_READ_ONLY", error!.Code);
    }

    [Fact]
    public async Task CreateLine_AtCentral_ReturnsCentralReadOnly()
    {
        var response = await CentralAdminClient().PostAsJsonAsync(
            $"/api/master-data/sites/{Guid.NewGuid()}/lines", new CreateLineRequest($"Line {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("CENTRAL_READ_ONLY", error!.Code);
    }

    [Fact]
    public async Task CreateMachine_AtCentral_ReturnsCentralReadOnly()
    {
        var response = await CentralAdminClient().PostAsJsonAsync(
            $"/api/master-data/lines/{Guid.NewGuid()}/machines", new CreateMachineRequest($"Machine {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("CENTRAL_READ_ONLY", error!.Code);
    }

    [Fact]
    public async Task CreateShiftSchedule_AtCentral_ReturnsCentralReadOnly()
    {
        var response = await CentralAdminClient().PostAsJsonAsync(
            $"/api/master-data/sites/{Guid.NewGuid()}/shift-schedules",
            new CreateShiftScheduleRequest($"Shift {Guid.NewGuid():N}", null, new TimeOnly(8, 0), new TimeOnly(16, 0)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("CENTRAL_READ_ONLY", error!.Code);
    }

    [Fact]
    public async Task CreateReasonCode_AtCentral_ReturnsCentralReadOnly()
    {
        var response = await CentralAdminClient().PostAsJsonAsync(
            $"/api/master-data/sites/{Guid.NewGuid()}/reason-codes",
            new { name = $"Reason {Guid.NewGuid():N}", lossCategory = "AvailabilityLoss" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("CENTRAL_READ_ONLY", error!.Code);
    }

    [Fact]
    public async Task CreateSite_AtSiteMode_StillSucceeds()
    {
        using var siteFactory = new SiteModeSyncApiFactory();
        var client = siteFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", siteFactory.CreateTokenFor("Admin"));

        var response = await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
