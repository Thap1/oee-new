namespace OeeNew.Infrastructure.MasterData;

/// <summary>
/// Bound from configuration section "Central". <see cref="SiteLinks"/> maps a Site.Id (string GUID) to
/// that site's own base URL, so Central's read-only Master Data view can link out to "Mở tại site X"
/// (Story 5.2, UX-DR5). Populated by ops when a site is onboarded — deliberately a static config map,
/// not a new synced `Site.BaseUrl` field: a handful of on-prem URLs that change only at
/// onboarding/decommission time doesn't justify rippling into Story 5.1's sync wire payload.
/// </summary>
public sealed class CentralOptions
{
    public const string SectionName = "Central";

    public Dictionary<string, string> SiteLinks { get; set; } = [];
}
