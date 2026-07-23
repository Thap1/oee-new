using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Api.Tests.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using OeeNew.Infrastructure.Persistence;
using Xunit;

namespace OeeNew.Api.Tests.Reports;

/// <summary>
/// Story 5.2, Task 1: proves the existing <c>OeeReportQueryUseCase</c>/report endpoint is already
/// site-agnostic — once Story 5.1 lands synced rows for multiple Sites into one local DB, a global-scope
/// report already aggregates across all of them with zero new backend query code.
/// </summary>
public class CrossSiteReportTests(MasterDataApiFactory factory) : IClassFixture<MasterDataApiFactory>
{
    private sealed record OeeReportResponse(
        string PeriodType, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
        double AvailabilityPercent, double PerformancePercent, double QualityPercent, double OeePercent,
        long AvailabilityLossSeconds, long PerformanceLossSeconds, long QualityLossSeconds,
        long UnattributedSeconds, int QualityRejectQuantity,
        Guid? TopDowntimeReasonCodeId, string? TopDowntimeReasonName, long? TopDowntimeReasonSeconds);

    [Fact]
    public async Task GetOeeReport_GlobalScope_SumsAvailabilityLossAcrossTwoDifferentSites()
    {
        var referenceDate = new DateOnly(2026, 7, 21);
        var windowStart = new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();

            var (siteA, lineA, machineA, reasonA) = SeedSiteChain(db, "Cross Site A");
            var (siteB, lineB, machineB, reasonB) = SeedSiteChain(db, "Cross Site B");
            db.SaveChanges();

            var eventA = new DowntimeEvent(Guid.Empty, machineA.Id, windowStart);
            eventA.AssignReason(reasonA.Id);
            eventA.Close(windowStart.AddSeconds(100));
            db.DowntimeEvents.Add(eventA);

            var eventB = new DowntimeEvent(Guid.Empty, machineB.Id, windowStart.AddMinutes(1));
            eventB.AssignReason(reasonB.Id);
            eventB.Close(windowStart.AddMinutes(1).AddSeconds(200));
            db.DowntimeEvents.Add(eventB);

            await db.SaveChangesAsync();
        }

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateTokenFor("Admin"));

        var report = await adminClient.GetFromJsonAsync<OeeReportResponse>(
            $"/api/reports/oee?periodType=Day&referenceDate={referenceDate:yyyy-MM-dd}");

        Assert.NotNull(report);
        Assert.True(report!.AvailabilityLossSeconds >= 300, "Expected combined loss from both sites (>= 300s) to be present in the global-scope report.");
    }

    private static (Site Site, Line Line, Machine Machine, ReasonCode Reason) SeedSiteChain(OeeDbContext db, string siteName)
    {
        var site = new Site(Guid.Empty, siteName);
        db.Sites.Add(site);
        db.SaveChanges();

        var line = new Line(Guid.Empty, $"{siteName} Line", site.Id);
        db.Lines.Add(line);
        db.SaveChanges();

        var machine = new Machine(Guid.Empty, $"{siteName} Machine", line.Id);
        db.Machines.Add(machine);

        var reason = new ReasonCode(Guid.Empty, site.Id, $"{siteName} Downtime", LossCategory.AvailabilityLoss);
        db.ReasonCodes.Add(reason);
        db.SaveChanges();

        return (site, line, machine, reason);
    }
}
