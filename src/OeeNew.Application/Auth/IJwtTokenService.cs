namespace OeeNew.Application.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Issues signed JWTs carrying the `role` (global for Admin, AD-7) and `siteId`/`lineId` claims.
/// Implemented in Infrastructure using the central Identity Provider's signing keys (JWKS, AD-7).
/// </summary>
public interface IJwtTokenService
{
    IssuedToken CreateToken(AuthenticatedUser user);
}
