namespace OeeNew.Domain.Identity;

/// <summary>Admin is global (no Site/Line scope); Manager/Operator/Viewer are scoped (AD-4).</summary>
public enum UserRole
{
    Admin,
    Manager,
    Operator,
    Viewer,
}
