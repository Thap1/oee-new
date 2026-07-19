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
}
