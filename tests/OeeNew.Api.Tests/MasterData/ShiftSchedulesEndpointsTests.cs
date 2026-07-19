using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

public class ShiftSchedulesEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private HttpClient AdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));
        return client;
    }

    private async Task<SiteResponse> CreateSiteAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest($"Site {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<SiteResponse>())!;

    [Fact]
    public async Task Create_WithExistingSite_ReturnsShiftWithGeneratedId()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var name = $"Shift {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(
            $"/api/master-data/sites/{site.Id}/shift-schedules",
            new CreateShiftScheduleRequest(name, null, new TimeOnly(8, 0), new TimeOnly(16, 0)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShiftScheduleResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(site.Id, body.SiteId);
        Assert.Equal(new TimeOnly(8, 0), body.StartTime);
        Assert.Equal(new TimeOnly(16, 0), body.EndTime);
    }

    [Fact]
    public async Task Create_WithUnknownSite_ReturnsBadRequestParentNotFound()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync(
            $"/api/master-data/sites/{Guid.NewGuid()}/shift-schedules",
            new CreateShiftScheduleRequest("Shift A", null, new TimeOnly(8, 0), new TimeOnly(16, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("PARENT_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task Create_OverlappingExistingShift_ReturnsConflictShiftOverlap()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        await client.PostAsJsonAsync(
            $"/api/master-data/sites/{site.Id}/shift-schedules",
            new CreateShiftScheduleRequest("Morning", null, new TimeOnly(8, 0), new TimeOnly(12, 0)));

        var response = await client.PostAsJsonAsync(
            $"/api/master-data/sites/{site.Id}/shift-schedules",
            new CreateShiftScheduleRequest("Overlap", null, new TimeOnly(10, 0), new TimeOnly(14, 0)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("SHIFT_OVERLAP", error!.Code);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var adminClient = AdminClient();
        var site = await CreateSiteAsync(adminClient);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));

        var response = await client.PostAsJsonAsync(
            $"/api/master-data/sites/{site.Id}/shift-schedules",
            new CreateShiftScheduleRequest("Blocked Shift", null, new TimeOnly(8, 0), new TimeOnly(16, 0)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_WithNonOverlappingTimes_UpdatesShift()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var shift = (await (await client.PostAsJsonAsync(
                $"/api/master-data/sites/{site.Id}/shift-schedules",
                new CreateShiftScheduleRequest($"Shift {Guid.NewGuid():N}", null, new TimeOnly(8, 0), new TimeOnly(16, 0))))
            .Content.ReadFromJsonAsync<ShiftScheduleResponse>())!;

        var response = await client.PutAsJsonAsync(
            $"/api/master-data/shift-schedules/{shift.Id}",
            new RescheduleShiftScheduleRequest(shift.Name, new TimeOnly(9, 0), new TimeOnly(17, 0)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShiftScheduleResponse>();
        Assert.Equal(new TimeOnly(9, 0), body!.StartTime);
        Assert.Equal(new TimeOnly(17, 0), body.EndTime);
    }

    [Fact]
    public async Task Delete_WithExistingShift_ReturnsNoContent()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var shift = (await (await client.PostAsJsonAsync(
                $"/api/master-data/sites/{site.Id}/shift-schedules",
                new CreateShiftScheduleRequest($"Shift {Guid.NewGuid():N}", null, new TimeOnly(8, 0), new TimeOnly(16, 0))))
            .Content.ReadFromJsonAsync<ShiftScheduleResponse>())!;

        var response = await client.DeleteAsync($"/api/master-data/shift-schedules/{shift.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ListBySite_ReturnsCreatedShift()
    {
        var client = AdminClient();
        var site = await CreateSiteAsync(client);
        var name = $"Shift {Guid.NewGuid():N}";
        await client.PostAsJsonAsync(
            $"/api/master-data/sites/{site.Id}/shift-schedules",
            new CreateShiftScheduleRequest(name, null, new TimeOnly(8, 0), new TimeOnly(16, 0)));

        var response = await client.GetAsync($"/api/master-data/sites/{site.Id}/shift-schedules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shifts = await response.Content.ReadFromJsonAsync<List<ShiftScheduleResponse>>();
        Assert.Contains(shifts!, s => s.Name == name);
    }
}
