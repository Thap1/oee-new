using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OeeNew.Application.Auth;
using OeeNew.Infrastructure.Identity;
using Xunit;

namespace OeeNew.Application.Tests.Auth;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions TestOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenLifetimeMinutes = 60,
    };

    private static (JwtTokenService Service, RsaJwtSigningKeyProvider Provider) CreateService()
    {
        var provider = new RsaJwtSigningKeyProvider();
        var service = new JwtTokenService(provider, Options.Create(TestOptions));
        return (service, provider);
    }

    [Fact]
    public void CreateToken_IncludesRoleAndSiteLineClaims()
    {
        var (service, provider) = CreateService();
        try
        {
            var user = new AuthenticatedUser(
                Guid.NewGuid(), "operator1", "Operator",
                SiteIds: [Guid.Parse("11111111-1111-1111-1111-111111111111")],
                LineIds: [Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333")]);

            var issued = service.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

            Assert.Equal("Operator", jwt.Claims.Single(c => c.Type == OeeClaimTypes.Role).Value);
            Assert.Equal(["11111111-1111-1111-1111-111111111111"],
                jwt.Claims.Where(c => c.Type == OeeClaimTypes.SiteId).Select(c => c.Value));
            Assert.Equal(2, jwt.Claims.Count(c => c.Type == OeeClaimTypes.LineId));
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public void CreateToken_ForAdmin_HasNoSiteOrLineClaims()
    {
        var (service, provider) = CreateService();
        try
        {
            var admin = new AuthenticatedUser(Guid.NewGuid(), "admin", "Admin", SiteIds: [], LineIds: []);

            var issued = service.CreateToken(admin);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

            Assert.DoesNotContain(jwt.Claims, c => c.Type == OeeClaimTypes.SiteId);
            Assert.DoesNotContain(jwt.Claims, c => c.Type == OeeClaimTypes.LineId);
            Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == OeeClaimTypes.Role).Value);
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public void CreateToken_IsVerifiableWithProviderPublicKey_AndHasKidHeader()
    {
        var (service, provider) = CreateService();
        try
        {
            var user = new AuthenticatedUser(Guid.NewGuid(), "admin", "Admin", SiteIds: [], LineIds: []);
            var issued = service.CreateToken(user);

            var handler = new JwtSecurityTokenHandler();
            var currentKey = provider.GetSigningKey();

            var principal = handler.ValidateToken(issued.AccessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = TestOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = TestOptions.Audience,
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(currentKey.Rsa) { KeyId = currentKey.KeyId },
            }, out var validatedToken);

            Assert.NotNull(principal.Identity);
            Assert.Equal(currentKey.KeyId, ((JwtSecurityToken)validatedToken).Header.Kid);
        }
        finally
        {
            provider.Dispose();
        }
    }

    [Fact]
    public void TokenIssuedBeforeRotation_StillValidatesAfterRotation_UsingPreviousKey()
    {
        // AC #3: token signed by the key before rotation must still be accepted while it is
        // within the provider's validation-key set (current + previous), i.e. the JWKS overlap window.
        var (service, provider) = CreateService();
        try
        {
            var user = new AuthenticatedUser(Guid.NewGuid(), "admin", "Admin", SiteIds: [], LineIds: []);
            var issued = service.CreateToken(user);
            var keyUsedToSign = provider.GetSigningKey();

            provider.RotateKey();

            var handler = new JwtSecurityTokenHandler();
            var validationKeys = provider.GetValidationKeys()
                .Select(k => (SecurityKey)new RsaSecurityKey(k.Rsa) { KeyId = k.KeyId })
                .ToList();

            Assert.Contains(validationKeys, k => k.KeyId == keyUsedToSign.KeyId);

            var principal = handler.ValidateToken(issued.AccessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = TestOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = TestOptions.Audience,
                ValidateLifetime = true,
                IssuerSigningKeys = validationKeys,
            }, out _);

            Assert.NotNull(principal.Identity);
        }
        finally
        {
            provider.Dispose();
        }
    }
}
