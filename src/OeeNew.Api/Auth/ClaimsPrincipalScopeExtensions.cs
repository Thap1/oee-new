using System.Security.Claims;
using OeeNew.Application.Auth;

namespace OeeNew.Api.Auth;

public static class ClaimsPrincipalScopeExtensions
{
    /// <summary>Derives the caller's <see cref="CallerScope"/> from the current request's own JWT claims (never from client-supplied parameters).</summary>
    public static CallerScope GetCallerScope(this ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(OeeClaimTypes.Role);
        if (role == "Admin")
        {
            return CallerScope.Global;
        }

        var siteIds = user.FindAll(OeeClaimTypes.SiteId).Select(c => Guid.Parse(c.Value)).ToList();
        var lineIds = user.FindAll(OeeClaimTypes.LineId).Select(c => Guid.Parse(c.Value)).ToList();
        return new CallerScope(IsGlobal: false, siteIds, lineIds);
    }
}
