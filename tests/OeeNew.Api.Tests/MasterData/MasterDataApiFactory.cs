using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Application.Auth;

namespace OeeNew.Api.Tests.MasterData;

/// <summary>
/// Points the API at the local `oeenew_test` Postgres database (separate from `oeenew_dev`) so
/// integration tests exercise the real EF Core + Postgres 18 stack (uuidv7 defaults, FK RESTRICT)
/// instead of a fake. Each test creates its own Site/Line/Machine tree with unique names, so tests
/// stay independent without needing shared-state cleanup between runs.
/// </summary>
public sealed class MasterDataApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=oeenew_test;Username=postgres;Password=1",
            });
        });
    }

    /// <summary>Mints a signed JWT for an arbitrary role without going through the bootstrap-admin login flow — used to test AC #4 (non-Admin write rejection) ahead of Story 1.4's real multi-user login.</summary>
    public string CreateTokenFor(string role)
    {
        using var scope = Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AuthenticatedUser(Guid.NewGuid(), $"{role.ToLowerInvariant()}-user", role, SiteIds: [], LineIds: []);
        return jwtTokenService.CreateToken(user).AccessToken;
    }
}
