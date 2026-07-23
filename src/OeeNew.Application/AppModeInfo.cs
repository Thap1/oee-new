namespace OeeNew.Application;

/// <summary>
/// AppMode: Site | Central (Architecture Spine AD-2), resolved once at startup and injected as a
/// singleton. Lives in Application (not Api) so use cases can depend on it directly (AD-1) — moved here
/// from Program.cs by Story 5.2 Task 2, once MasterData use cases (not just Story 5.1's Api-layer
/// SyncController) needed to read it.
/// </summary>
public sealed record AppModeInfo(string Mode)
{
    public bool IsCentral => Mode == "Central";
}
