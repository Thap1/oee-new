using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.Identity;
using Xunit;

namespace OeeNew.Api.Tests.Identity;

public class UserEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    // UserRole serializes as a string on the server (Program.cs JsonStringEnumConverter); the test's
    // plain HttpClient needs the same converter to deserialize UserResponse.Role back into the enum.
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

    private async Task<Guid> CreateSiteAsync(HttpClient client)
    {
        var name = $"Site {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/master-data/sites", new CreateSiteRequest(name));
        return (await response.Content.ReadFromJsonAsync<SiteResponse>())!.Id;
    }

    private async Task<Guid> CreateLineAsync(HttpClient client, Guid siteId)
    {
        var name = $"Line {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/lines", new CreateLineRequest(name));
        return (await response.Content.ReadFromJsonAsync<LineResponse>())!.Id;
    }

    [Fact]
    public async Task Create_OperatorWithSiteAndLine_ReturnsUserWithMatchingScope()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var lineId = await CreateLineAsync(client, siteId);
        var username = $"op-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest(username, "Passw0rd!", UserRole.Operator, [siteId], [lineId]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(UserRole.Operator, body!.Role);
        Assert.Equal([siteId], body.SiteIds);
        Assert.Equal([lineId], body.LineIds);
    }

    [Fact]
    public async Task Create_OperatorWithoutLine_ReturnsValidationError()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var username = $"op-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest(username, "Passw0rd!", UserRole.Operator, [siteId], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    [Fact]
    public async Task Create_DuplicateUsername_ReturnsConflict()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var username = $"mgr-{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/users", new CreateUserRequest(username, "Passw0rd!", UserRole.Manager, [siteId], []));

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(username, "Passw0rd!", UserRole.Manager, [siteId], []));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("USERNAME_TAKEN", error!.Code);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager"));

        var response = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("blocked-user", "Passw0rd!", UserRole.Viewer, [Guid.NewGuid()], []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public async Task List_AsNonAdmin_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Viewer"));

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoleAndScope_ChangesRole_ReturnsUpdatedUser()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var username = $"mgr-{Guid.NewGuid():N}";
        var created = (await (await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest(username, "Passw0rd!", UserRole.Manager, [siteId], [])))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions))!;

        var response = await client.PutAsJsonAsync($"/api/users/{created.Id}",
            new UpdateUserRoleRequest(UserRole.Viewer, [siteId], []));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(UserRole.Viewer, updated!.Role);
    }
}
