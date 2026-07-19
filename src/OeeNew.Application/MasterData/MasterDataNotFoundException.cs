namespace OeeNew.Application.MasterData;

/// <summary>Thrown when a Site/Line/Machine lookup by Id finds no record.</summary>
public sealed class MasterDataNotFoundException(string entityName, Guid id)
    : Exception($"{entityName} '{id}' was not found.")
{
    public string EntityName { get; } = entityName;
    public Guid Id { get; } = id;
}
