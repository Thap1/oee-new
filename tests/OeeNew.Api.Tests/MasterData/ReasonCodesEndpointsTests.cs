using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OeeNew.Api.Controllers;
using OeeNew.Api.Errors;
using OeeNew.Domain.MasterData;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.MasterData;

public class ReasonCodesEndpointsTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    // LossCategory serializes as a string on the server (Program.cs JsonStringEnumConverter); the
    // test's plain HttpClient needs the same converter to deserialize ReasonCodeResponse back.
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

    [Fact]
    public async Task Create_WithLossCategory_ReturnsActiveReasonCode()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var name = $"Reason {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/reason-codes",
            new CreateReasonCodeRequest(name, LossCategory.QualityLoss));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(LossCategory.QualityLoss, body!.LossCategory);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Create_WithoutLossCategory_ReturnsValidationError()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var name = $"Reason {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/reason-codes",
            new CreateReasonCodeRequest(name, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    [Fact]
    public async Task Create_AsNonAdmin_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Manager"));

        var response = await client.PostAsJsonAsync($"/api/master-data/sites/{Guid.NewGuid()}/reason-codes",
            new CreateReasonCodeRequest("Blocked", LossCategory.AvailabilityLoss));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse_ButKeepsRecord()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var created = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var response = await client.PutAsync($"/api/master-data/reason-codes/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions);
        Assert.False(updated!.IsActive);

        var listResponse = await client.GetAsync($"/api/master-data/sites/{siteId}/reason-codes");
        var list = await listResponse.Content.ReadFromJsonAsync<List<ReasonCodeResponse>>(JsonOptions);
        Assert.Contains(list!, r => r.Id == created.Id);
    }

    [Fact]
    public async Task Deactivate_AsNonAdmin_ReturnsForbidden()
    {
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);
        var created = (await (await client.PostAsJsonAsync($"/api/master-data/sites/{siteId}/reason-codes",
            new CreateReasonCodeRequest($"Reason {Guid.NewGuid():N}", LossCategory.AvailabilityLoss)))
            .Content.ReadFromJsonAsync<ReasonCodeResponse>(JsonOptions))!;

        var nonAdminClient = factory.CreateClient();
        nonAdminClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Operator"));
        var response = await nonAdminClient.PutAsync($"/api/master-data/reason-codes/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RawSqlInsert_WithoutLossCategory_IsRejectedByDbConstraint()
    {
        // AC #1: "kể cả gọi API trực tiếp" — NOT NULL must be enforced at the DB schema level, not
        // just by the Application layer. Bypass the API/EF change-tracking entirely with raw SQL.
        var client = AdminClient();
        var siteId = await CreateSiteAsync(client);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();

        await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"ReasonCode\" (\"SiteId\", \"Name\", \"IsActive\") VALUES ({siteId}, {"Missing category"}, true)"));
    }
}
