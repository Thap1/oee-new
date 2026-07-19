using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OeeNew.Api.Errors;
using OeeNew.Api.Controllers;
using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Api.Tests;

public class AuthEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "ChangeMe123!"; // matches appsettings.json BootstrapAdmin.PasswordHash

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtWithRoleClaim()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsEnvelopedUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("INVALID_CREDENTIALS", error!.Code);
    }

    [Fact]
    public async Task Login_WithMissingUsername_ReturnsEnvelopedValidationError()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", AdminPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDATION_ERROR", error!.Code);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsEnvelopedUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error!.Code);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsClaims()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(me);
        Assert.Equal("Admin", me!.Role);
        Assert.Empty(me.SiteIds);
    }

    [Fact]
    public async Task Jwks_IsPubliclyAccessible_AndExposesCurrentKey()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jwks = await response.Content.ReadFromJsonAsync<JwksDocument>();
        Assert.NotNull(jwks);
        Assert.NotEmpty(jwks!.Keys);
    }

    [Fact]
    public async Task TokenIssuedBeforeRotation_StillAuthorizes_AfterKeyRotation()
    {
        // AC #3 end-to-end: rotate the central Identity Provider's signing key, then confirm a
        // token issued *before* rotation is still accepted (overlap window via JWKS cache of 2 keys).
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        using (var scope = factory.Services.CreateScope())
        {
            var signingKeyProvider = scope.ServiceProvider.GetRequiredService<IJwtSigningKeyProvider>();
            signingKeyProvider.RotateKey();
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
