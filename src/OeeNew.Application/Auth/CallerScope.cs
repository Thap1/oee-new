namespace OeeNew.Application.Auth;

/// <summary>
/// The calling user's access scope, derived from their JWT claims (Story 1.6, AC #2/#4 — FR-015,
/// NFR-5). Admin is global (<see cref="IsGlobal"/>, AD-7); Manager/Operator/Viewer are restricted to
/// their <see cref="SiteIds"/> (and, when set, further to specific <see cref="LineIds"/>).
/// Server-side enforcement always re-derives this from the request's own JWT — never from a
/// client-supplied parameter.
/// </summary>
public sealed record CallerScope(bool IsGlobal, IReadOnlyList<Guid> SiteIds, IReadOnlyList<Guid> LineIds)
{
    public static readonly CallerScope Global = new(true, [], []);

    public bool AllowsSite(Guid siteId) => IsGlobal || SiteIds.Contains(siteId);

    /// <summary>True unless the caller is explicitly Line-restricted (Operator) to a different Line. Assumes site-level access is already confirmed via <see cref="AllowsSite"/>.</summary>
    public bool AllowsLine(Guid lineId) => IsGlobal || LineIds.Count == 0 || LineIds.Contains(lineId);
}
