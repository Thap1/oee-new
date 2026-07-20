namespace OeeNew.Domain.MasterData;

/// <summary>
/// Downtime reason code under a Site, tagged with exactly one <see cref="LossCategory"/> (AD-5) so
/// downtime rolls up correctly into the OEE formula (Epic 3/4). Deactivated instead of hard-deleted
/// (Story 1.5, AC #2) — hard-delete-when-referenced is Story 2.5's concern, once DowntimeEvent exists.
/// </summary>
public sealed class ReasonCode
{
    private const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public string Name { get; private set; }
    public LossCategory LossCategory { get; private set; }
    public bool IsActive { get; private set; }

    public ReasonCode(Guid id, Guid siteId, string name, LossCategory lossCategory)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Reason code requires a valid parent Site.", nameof(siteId));
        }

        if (!Enum.IsDefined(lossCategory))
        {
            throw new MasterDataValidationException("LossCategory must be one of AvailabilityLoss, PerformanceLoss, QualityLoss.");
        }

        Id = id;
        SiteId = siteId;
        Name = ValidateName(name);
        LossCategory = lossCategory;
        IsActive = true;
    }

    /// <summary>Hides this reason code from the Operator's picker (Story 2.5) without touching history (AC #2).</summary>
    public void Deactivate() => IsActive = false;

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MasterDataValidationException("Reason code name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new MasterDataValidationException($"Reason code name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
