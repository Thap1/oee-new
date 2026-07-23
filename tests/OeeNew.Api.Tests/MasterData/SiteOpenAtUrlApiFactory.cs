using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Application.Auth;

namespace OeeNew.Api.Tests.MasterData;

/// <summary>Test host with one `Central:SiteLinks` entry configured for <see cref="LinkedSiteId"/> (Story 5.2, Task 5's "Open at Site X" link) — <see cref="AppMode"/> is settable before first use so the same SiteLinks config can be exercised under both AppModes (the Site-mode variant proves `openAtUrl` stays null regardless of config).</summary>
public sealed class SiteOpenAtUrlApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid LinkedSiteId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    public const string LinkedSiteUrl = "https://site-a.oee.local";

    public string AppMode { get; set; } = "Central";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=oeenew_test;Username=postgres;Password=1",
                ["AppMode"] = AppMode,
                [$"Central:SiteLinks:{LinkedSiteId}"] = LinkedSiteUrl,
            });
        });
    }

    public string CreateTokenFor(string role) => CreateTokenFor(role, siteIds: [], lineIds: []);

    public string CreateTokenFor(string role, Guid[] siteIds, Guid[] lineIds)
    {
        using var scope = Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AuthenticatedUser(Guid.NewGuid(), $"{role.ToLowerInvariant()}-user", role, SiteIds: siteIds, LineIds: lineIds);
        return jwtTokenService.CreateToken(user).AccessToken;
    }
}
