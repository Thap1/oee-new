using OeeNew.Application;

namespace OeeNew.Application.MasterData;

/// <summary>Shared Admin-role guard for Site/Line/Machine write use cases (AC #4, FR-015/NFR-5).</summary>
internal static class MasterDataAuthorization
{
    public const string AdminRole = "Admin";

    public static void EnsureAdmin(string? callerRole)
    {
        if (callerRole != AdminRole)
        {
            throw new MasterDataForbiddenException();
        }
    }

    /// <summary>AD-4: Site/Line/Machine/ShiftSchedule/ReasonCode master data is owned and writable only at its origin Site, never proxied through Central (Story 5.2, UX-DR5) — a distinct rule from <see cref="EnsureAdmin"/>'s role check, and deliberately not applied to Users (AD-7).</summary>
    public static void EnsureNotCentral(AppModeInfo appMode)
    {
        if (appMode.IsCentral)
        {
            throw new CentralReadOnlyException();
        }
    }
}
