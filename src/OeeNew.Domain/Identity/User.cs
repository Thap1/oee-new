using OeeNew.Domain.MasterData;

namespace OeeNew.Domain.Identity;

/// <summary>
/// Site-local user with role + Site/Line scoping (Story 1.4, AD-4: role-scoping is written at the
/// site, not the central Identity Provider). <see cref="PasswordHash"/> is the credential issued by
/// the central Identity Provider once, at creation time (AD-7) — see
/// <c>UserManagementUseCase</c>/<c>ICentralCredentialProvisioner</c> for that provisioning seam.
/// </summary>
public sealed class User
{
    private const int MaxUsernameLength = 100;

    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public UserRole Role { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid[] SiteIds { get; private set; }
    public Guid[] LineIds { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// EF Core materialization only (Story 1.4 review) — deliberately bypasses domain validation so
    /// reading an existing row can never throw. EF's constructor-binding convention greedily prefers
    /// the constructor with the most bindable parameters, so this parameterless one is only picked up
    /// when explicitly selected — see <c>OeeDbContext</c>'s <c>HasConstructorBinding</c> for <see cref="User"/>.
    /// Never call this directly; use the validating constructor below or <see cref="Rescope"/>.
    /// </summary>
    private User()
    {
        Username = string.Empty;
        PasswordHash = string.Empty;
        SiteIds = [];
        LineIds = [];
    }

    public User(Guid id, string username, UserRole role, string passwordHash, Guid[] siteIds, Guid[] lineIds)
    {
        Id = id;
        Username = ValidateUsername(username);
        PasswordHash = ValidatePasswordHash(passwordHash);
        Role = default;
        SiteIds = [];
        LineIds = [];
        IsActive = true;
        Rescope(role, siteIds, lineIds);
    }

    /// <summary>Change role and/or Site/Line scoping (Task 3: "sửa user + đổi role").</summary>
    public void Rescope(UserRole role, Guid[] siteIds, Guid[] lineIds)
    {
        var distinctSiteIds = siteIds.Distinct().ToArray();
        var distinctLineIds = lineIds.Distinct().ToArray();
        ValidateScoping(role, distinctSiteIds, distinctLineIds);

        Role = role;
        SiteIds = distinctSiteIds;
        LineIds = distinctLineIds;
    }

    /// <summary>Hide this user from login (Story 1.4 review, mirrors <c>ReasonCode.Deactivate</c>) without deleting role/scope history.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Replace the credential hash after a rehash (e.g. <c>PasswordVerificationResult.SuccessRehashNeeded</c>). Does not touch role/scope.</summary>
    public void UpdatePasswordHash(string passwordHash) => PasswordHash = ValidatePasswordHash(passwordHash);

    /// <summary>Validate a role/scope combination without constructing a <see cref="User"/> — lets callers fail fast before doing anything else (e.g. before provisioning a credential).</summary>
    public static void ValidateRoleAndScope(UserRole role, Guid[] siteIds, Guid[] lineIds) =>
        ValidateScoping(role, siteIds.Distinct().ToArray(), lineIds.Distinct().ToArray());

    private static void ValidateScoping(UserRole role, IReadOnlyList<Guid> siteIds, IReadOnlyList<Guid> lineIds)
    {
        if (role == UserRole.Admin)
        {
            if (siteIds.Count > 0 || lineIds.Count > 0)
            {
                throw new MasterDataValidationException("Admin is a global role and cannot be scoped to a Site or Line.");
            }

            return;
        }

        if (siteIds.Count == 0)
        {
            throw new MasterDataValidationException($"{role} must be assigned to at least one Site.");
        }

        if (role == UserRole.Operator)
        {
            if (lineIds.Count == 0)
            {
                throw new MasterDataValidationException("Operator must be assigned to at least one Line.");
            }
        }
        else if (lineIds.Count > 0)
        {
            throw new MasterDataValidationException($"{role} is scoped to Site only and cannot be assigned to a Line.");
        }
    }

    private static string ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new MasterDataValidationException("Username is required.");
        }

        var trimmed = username.Trim();
        if (trimmed.Length > MaxUsernameLength)
        {
            throw new MasterDataValidationException($"Username must be at most {MaxUsernameLength} characters.");
        }

        return trimmed;
    }

    private static string ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new MasterDataValidationException("Password hash is required.");
        }

        return passwordHash;
    }
}
