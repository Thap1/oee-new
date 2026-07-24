using System.Net;
using System.Net.Http.Json;
using OeeNew.Api.Tests.MasterData;
using Xunit;

namespace OeeNew.Api.Tests.Sync;

/// <summary>Story 5.3: `GET /api/sync/status` is a normal JWT-authenticated (`ReportsAccess`) endpoint — deliberately NOT sharing Story 5.1's `SyncController`'s anonymous/API-key gate.</summary>
public class SyncStatusEndpointTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private sealed record SiteSyncStatusResponse(Guid SiteId, string SiteName, DateTimeOffset? LastSyncedAt, bool IsStale);

    [Fact]
    public async Task Get_OperatorToken_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));

        var response = await client.GetAsync("/api/sync/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Admin")]
    public async Task Get_AllowedRoles_ReturnOkWithCorrectShape(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor(role));

        var response = await client.GetAsync("/api/sync/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<SiteSyncStatusResponse>>();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task Get_NoAuthorizationHeader_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/sync/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
