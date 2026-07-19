namespace OeeNew.Application.MasterData;

/// <summary>
/// Thrown when creating a Line/Machine that references a parent Site/Line which does not exist
/// (Story 1.2 AC #2/#3 — "không tạo được nếu thiếu Site cha hợp lệ").
/// </summary>
public sealed class MasterDataParentNotFoundException(string parentEntityName, Guid parentId)
    : Exception($"{parentEntityName} '{parentId}' was not found.")
{
    public string ParentEntityName { get; } = parentEntityName;
    public Guid ParentId { get; } = parentId;
}
