namespace OeeNew.Application.MasterData;

/// <summary>
/// Thrown when deleting a Site/Line that still has child Lines/Machines. No cascade-delete in the
/// MVP (Story 1.2 AC #5, explicit `[ASSUMPTION]`) — the caller must remove dependents first.
/// </summary>
public sealed class MasterDataHasDependentsException(string entityName, Guid id, IReadOnlyList<string> dependentNames)
    : Exception($"{entityName} '{id}' still has {dependentNames.Count} dependent record(s).")
{
    public string EntityName { get; } = entityName;
    public Guid Id { get; } = id;
    public IReadOnlyList<string> DependentNames { get; } = dependentNames;
}
