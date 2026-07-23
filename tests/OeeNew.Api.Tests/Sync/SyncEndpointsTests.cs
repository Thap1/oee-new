using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Application.Sync;
using OeeNew.Domain.MasterData;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.Sync;

public class SyncEndpointsTests(CentralSyncApiFactory factory) : IClassFixture<CentralSyncApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient ClientWithKey(string? apiKey)
    {
        var client = factory.CreateClient();
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Sync-Api-Key", apiKey);
        }

        return client;
    }

    private static SyncBatch BuildBatch(Guid siteId, Guid lineId, Guid machineId, Guid reasonCodeId, Guid downtimeEventId, Guid qualityRejectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new SyncBatch(
            [new SyncSiteRecord(siteId, "Sync Site")],
            [new SyncLineRecord(lineId, "Sync Line", siteId)],
            [new SyncMachineRecord(machineId, "Sync Machine", lineId)],
            [new SyncReasonCodeRecord(reasonCodeId, siteId, "Sync Reason", LossCategory.AvailabilityLoss, true)],
            [new SyncDowntimeEventRecord(downtimeEventId, machineId, reasonCodeId, now.AddMinutes(-5), now.AddMinutes(-4))],
            [new SyncQualityRejectRecord(qualityRejectId, machineId, 2, now)]);
    }

    [Fact]
    public async Task ReceiveBatch_CorrectApiKeyAgainstCentralHost_SucceedsAndPersistsRows()
    {
        var client = ClientWithKey(CentralSyncApiFactory.TestApiKey);
        var siteId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var reasonCodeId = Guid.NewGuid();
        var downtimeEventId = Guid.NewGuid();
        var qualityRejectId = Guid.NewGuid();
        var batch = BuildBatch(siteId, lineId, machineId, reasonCodeId, downtimeEventId, qualityRejectId);

        var response = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        Assert.True(await db.Sites.AnyAsync(s => s.Id == siteId));
        Assert.True(await db.DowntimeEvents.AnyAsync(e => e.Id == downtimeEventId));
        Assert.True(await db.QualityRejects.AnyAsync(q => q.Id == qualityRejectId));

        var syncStatus = await db.SiteSyncStatuses.SingleAsync(s => s.SiteId == siteId);
        Assert.True((DateTimeOffset.UtcNow - syncStatus.LastSyncedAt).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ReceiveBatch_IngestingSameBatchTwice_IsIdempotent()
    {
        var client = ClientWithKey(CentralSyncApiFactory.TestApiKey);
        var siteId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var reasonCodeId = Guid.NewGuid();
        var downtimeEventId = Guid.NewGuid();
        var qualityRejectId = Guid.NewGuid();
        var batch = BuildBatch(siteId, lineId, machineId, reasonCodeId, downtimeEventId, qualityRejectId);

        var first = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);
        var second = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        Assert.Equal(1, await db.DowntimeEvents.CountAsync(e => e.Id == downtimeEventId));
        Assert.Equal(1, await db.QualityRejects.CountAsync(q => q.Id == qualityRejectId));
        Assert.Equal(1, await db.Sites.CountAsync(s => s.Id == siteId));
    }

    [Fact]
    public async Task ReceiveBatch_MissingApiKey_ReturnsUnauthorized()
    {
        var client = ClientWithKey(apiKey: null);
        var batch = BuildBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveBatch_WrongApiKey_ReturnsUnauthorized()
    {
        var client = ClientWithKey("wrong-key");
        var batch = BuildBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveBatch_AgainstSiteModeHost_ReturnsNotFoundRegardlessOfKey()
    {
        using var siteFactory = new SiteModeSyncApiFactory();
        var client = siteFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Api-Key", CentralSyncApiFactory.TestApiKey);
        var batch = BuildBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/sync/batch", batch, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
