namespace OeeNew.Domain.MasterData;

/// <summary>
/// Belongs to a Site (Architecture Spine — Core-Entity ERD: SITE ||--o{ LINE).
/// <see cref="SiteId"/> must reference an already-persisted Site — a Line cannot exist without a
/// valid parent (Story 1.2 AC #2).
/// </summary>
public sealed class Line
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid SiteId { get; private set; }

    public Line(Guid id, string name, Guid siteId)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Line requires a valid parent Site.", nameof(siteId));
        }

        Id = id;
        Name = ValidateName(name);
        SiteId = siteId;
    }

    public void Rename(string name) => Name = ValidateName(name);

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MasterDataValidationException("Line name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new MasterDataValidationException($"Line name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
