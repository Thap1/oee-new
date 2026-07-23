using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OeeNew.Api.Tests.Sync;

/// <summary>
/// AppMode=Site test host with the same valid API key as <see cref="CentralSyncApiFactory"/> — isolates
/// the "wrong AppMode" 404 guard in <c>SyncController</c> from the API-key check, so a Site-mode request
/// with a *correct* key still gets 404 (Story 5.1, AC #4's "no Central endpoint should ever accept
/// a payload it has no reason to store" reasoning applied symmetrically to a Site instance).
/// </summary>
public sealed class SiteModeSyncApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=oeenew_test;Username=postgres;Password=1",
                ["AppMode"] = "Site",
                ["Sync:ApiKey"] = CentralSyncApiFactory.TestApiKey,
            });
        });
    }
}
