namespace OeeNew.Domain.MasterData;

/// <summary>
/// Belongs to a Line (Architecture Spine — Core-Entity ERD: LINE ||--o{ MACHINE).
/// <see cref="LineId"/> must reference an already-persisted Line — a Machine cannot exist without
/// a valid parent (Story 1.2 AC #3).
/// </summary>
public sealed class Machine
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid LineId { get; private set; }

    public Machine(Guid id, string name, Guid lineId)
    {
        if (lineId == Guid.Empty)
        {
            throw new ArgumentException("Machine requires a valid parent Line.", nameof(lineId));
        }

        Id = id;
        Name = ValidateName(name);
        LineId = lineId;
    }

    public void Rename(string name) => Name = ValidateName(name);

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MasterDataValidationException("Machine name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new MasterDataValidationException($"Machine name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
