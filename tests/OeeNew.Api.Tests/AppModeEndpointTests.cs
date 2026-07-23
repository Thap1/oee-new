using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Api.Tests.Sync;
using Xunit;

namespace OeeNew.Api.Tests;

/// <summary>Story 5.2, Task 5: `GET /api/app-mode` is anonymous (readable before a JWT exists) and reports the host's configured AppMode.</summary>
public class AppModeEndpointTests
{
    private sealed record AppModeResponse(string Mode);

    [Fact]
    public async Task Get_NoAuthorizationHeader_SucceedsAndReturnsSiteMode()
    {
        await using var factory = new MasterDataApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-mode");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AppModeResponse>();
        Assert.Equal("Site", body!.Mode);
    }

    [Fact]
    public async Task Get_AgainstCentralModeHost_ReturnsCentralMode()
    {
        await using var factory = new CentralSyncApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/app-mode");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AppModeResponse>();
        Assert.Equal("Central", body!.Mode);
    }
}
