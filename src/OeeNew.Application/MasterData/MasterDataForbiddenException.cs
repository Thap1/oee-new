namespace OeeNew.Application.MasterData;

/// <summary>
/// Thrown when a caller without the Admin role invokes a Site/Line/Machine write use case.
/// Application-layer re-check of the same rule the API enforces via `[Authorize(Policy = "AdminOnly")]`
/// (Architecture Spine — Consistency Conventions; Story 1.2 Dev Notes, FR-015/NFR-5).
/// </summary>
public sealed class MasterDataForbiddenException() : Exception("Admin role is required for this operation.");
