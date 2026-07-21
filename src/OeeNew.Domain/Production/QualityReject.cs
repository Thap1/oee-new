using OeeNew.Domain.MasterData;

namespace OeeNew.Domain.Production;

/// <summary>
/// Basic reject/scrap quantity for a Machine (Story 2.6, FR-010) — an append-only log entry, no
/// root-cause detail (explicitly out of MVP scope per the PRD's own `[ASSUMPTION]`). No mutation
/// methods: nothing in this story's AC edits or deletes a past record.
/// </summary>
public sealed class QualityReject
{
    public Guid Id { get; private set; }
    public Guid MachineId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public QualityReject(Guid id, Guid machineId, int quantity, DateTimeOffset recordedAt)
    {
        if (quantity <= 0)
        {
            throw new MasterDataValidationException("Quantity must be greater than zero.");
        }

        Id = id;
        MachineId = machineId;
        Quantity = quantity;
        RecordedAt = recordedAt;
    }
}
