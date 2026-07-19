using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OeeNew.Application.Auth;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Issues JWTs signed by the central Identity Provider's current RSA key (AD-7).
/// Embeds `kid` in the header so validators (this instance or a remote site, once Sync/JWKS
/// fetch exists) can pick the matching public key from the JWKS document.
/// </summary>
public sealed class JwtTokenService(IJwtSigningKeyProvider signingKeyProvider, IOptions<JwtOptions> options) : IJwtTokenService
{
    public IssuedToken CreateToken(AuthenticatedUser user)
    {
        var jwtOptions = options.Value;
        var signingKey = signingKeyProvider.GetSigningKey();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(OeeClaimTypes.Role, user.Role),
        };
        claims.AddRange(user.SiteIds.Select(siteId => new Claim(OeeClaimTypes.SiteId, siteId.ToString())));
        claims.AddRange(user.LineIds.Select(lineId => new Claim(OeeClaimTypes.LineId, lineId.ToString())));

        var credentials = new SigningCredentials(new RsaSecurityKey(signingKey.Rsa) { KeyId = signingKey.KeyId }, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedToken(accessToken, expires);
    }
}
