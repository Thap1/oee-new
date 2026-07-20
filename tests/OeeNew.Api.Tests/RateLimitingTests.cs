using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OeeNew.Api.Controllers;
using Xunit;

namespace OeeNew.Api.Tests;

// Own WebApplicationFactory instance (not shared with AuthEndpointsTests) so this test's burst of
// requests doesn't consume the login rate limiter's quota for other tests in the suite.
public class RateLimitingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Login_ExceedingRateLimit_ReturnsEnvelopedTooManyRequests()
    {
        var client = factory.CreateClient();
        var request = new LoginRequest("admin", "wrong-password");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login", request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        var error = await last.Content.ReadFromJsonAsync<OeeNew.Api.Errors.ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("TOO_MANY_REQUESTS", error!.Code);
    }
}
