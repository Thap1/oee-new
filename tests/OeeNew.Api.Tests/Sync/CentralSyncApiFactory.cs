using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OeeNew.Api.Tests.Sync;

/// <summary>AppMode=Central test host for the sync receive endpoint (Story 5.1) — same real-Postgres approach as <c>MasterDataApiFactory</c>, but with AppMode flipped and a fixed test API key configured.</summary>
public sealed class CentralSyncApiFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-sync-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=oeenew_test;Username=postgres;Password=1",
                ["AppMode"] = "Central",
                ["Sync:ApiKey"] = TestApiKey,
            });
        });
    }
}
