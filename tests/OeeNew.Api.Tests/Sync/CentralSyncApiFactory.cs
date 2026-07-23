using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Application.Auth;

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

    /// <summary>Mints a signed JWT for an arbitrary role against this Central-mode host, same shape as <c>MasterDataApiFactory.CreateTokenFor</c> (Story 5.2's Central-read-only endpoint tests need an Admin token here too).</summary>
    public string CreateTokenFor(string role) => CreateTokenFor(role, siteIds: [], lineIds: []);

    public string CreateTokenFor(string role, Guid[] siteIds, Guid[] lineIds)
    {
        using var scope = Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AuthenticatedUser(Guid.NewGuid(), $"{role.ToLowerInvariant()}-user", role, SiteIds: siteIds, LineIds: lineIds);
        return jwtTokenService.CreateToken(user).AccessToken;
    }
}
