namespace OeeNew.Application.Auth;

/// <summary>
/// Orchestrates login: validate credentials via the central Identity Provider (AD-7),
/// then issue a JWT carrying role + siteId/lineId claims. No direct EF Core/HTTP access here —
/// only calls IUserAuthenticator / IJwtTokenService abstractions (AD-1).
/// </summary>
public sealed class LoginUseCase(IUserAuthenticator userAuthenticator, IJwtTokenService jwtTokenService)
{
    public async Task<IssuedToken> ExecuteAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await userAuthenticator.ValidateCredentialsAsync(username, password, cancellationToken)
            ?? throw new InvalidCredentialsException();

        return jwtTokenService.CreateToken(user);
    }
}
