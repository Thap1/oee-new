namespace OeeNew.Domain.MasterData;

/// <summary>
/// Top of the Site &gt; Line &gt; Machine hierarchy (Architecture Spine — Core-Entity ERD).
/// <see cref="Id"/> is <see cref="Guid.Empty"/> until persisted — the Postgres column default
/// (`uuidv7()`, AD-6) assigns the real value on insert, not application code.
/// </summary>
public sealed class Site
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }

    public Site(Guid id, string name)
    {
        Id = id;
        Name = ValidateName(name);
    }

    public void Rename(string name) => Name = ValidateName(name);

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MasterDataValidationException("Site name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new MasterDataValidationException($"Site name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
